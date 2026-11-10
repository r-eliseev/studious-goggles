# 0016. Semantic search: where vectors are computed and where they are stored

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-29

## Context
Task 079 gives fuzzy word search: over titles, prompts and picture descriptions. The owner wants search by meaning — over texts and over the pictures themselves ("find all pictures with a lighthouse" without the word in the prompt) (`tasks/backlog/081-semantic-search-research.md`). Principles: BYOK (the user's provider keys), privacy (prompts and pictures go only to the providers the user chose, F10.6), working on Android and iOS, new packages only through an ADR.

Model and price data was taken on 2026-10-29: the OpenRouter embeddings catalog (`https://openrouter.ai/api/v1/embeddings/models`, 38 models) and the `POST /embeddings` schema from `https://openrouter.ai/openapi.json`; licenses and files from the Hugging Face cards. Speed measurements: `poc/backlog/081-semantic-search-research/` (README there).

## What is already known

**The OpenRouter embeddings API** (`POST /embeddings`): `input` is a string, an array of strings or an array of objects `{ "content": [ { "type": "text", "text": … } | { "type": "image_url", "image_url": { "url": … } } ] }`. Answer: `data[].embedding` (an array of numbers or base64), `usage.cost` (cost in credits; present in the schema for embeddings, the exact unit is not verified by a call). Per the schema the `session_id` field is not passed to the provider (it is used for the OpenRouter log); the purpose of the `user` field is not explained in the schema description.

**OpenRouter models** (price per 1M tokens, the `prompt` field; context in tokens):

| Model | Input | Price, $/1M tokens | Image price | Context | Note |
|---|---|---|---|---|---|
| `baai/bge-m3` | text | 0.01 | — | 8 194 | "optimized for multilingual retrieval" (model description) |
| `intfloat/multilingual-e5-large` | text | 0.01 | — | 512 | multilingual |
| `openai/text-embedding-3-small` | text | 0.02 | — | 8 192 | — |
| `qwen/qwen3-embedding-4b` / `-8b` | text | 0.02 / 0.01 | — | 32 768 / 40 960 | — |
| `voyageai/voyage-4-lite` / `voyage-4` | text | 0.02 / 0.06 | — | 32 000 | — |
| `voyageai/voyage-multimodal-3.5` | text + image | 0.12 | not given | 32 000 | text, images and video in one space |
| `google/gemini-embedding-2` (and `-preview`) | text, image, file, audio, video | 0.20 | 0.45 (unit not given in the catalog) | 8 192 | "text and images in a single vector space" |
| `amazon/titan-embed-image-v1` | text + image | 0.80 | 0.00006 (probably per image; not verified) | 128 | Titan Multimodal G1 |
| `nvidia/nemotron-3.5-embed-*:free`, `nvidia/llama-nemotron-embed-vl-1b-v2:free` | text (and image for the last two) | 0 | 0 | 32 768–131 072 | free; stability and terms not verified |

Seven catalog models accept images. 31 models fit text search without images.

**Local models** (Hugging Face, 2026-10-29):

| Model | License (HF metadata) | ONNX file | Size |
|---|---|---|---|
| `intfloat/multilingual-e5-small` | MIT | `onnx/model_O4.onnx` | 235 MB (fp32: 470 MB; quantized for AVX-512 VNNI: 118 MB) |
| `BAAI/bge-m3` | MIT | `onnx/model.onnx` + `model.onnx_data` | ≈2.3 GB (fp32) |
| `sentence-transformers/clip-ViT-B-32-multilingual-v1` (text encoder, CLIP-compatible) | Apache-2.0 | `onnx/model_O4.onnx` | 269 MB (fp32: 539 MB) |
| `Xenova/clip-vit-base-patch32` (ONNX conversion of CLIP ViT-B/32) | MIT | `vision_model.onnx` | 352 MB (quantized: 89 MB) |
| `openai/clip-vit-base-patch32` (original) | not given in the HF metadata | — | — |
| `google/siglip2-base-patch16-224` | Apache-2.0 | no ONNX in the repository | — |
| `jinaai/jina-clip-v2` | CC-BY-NC-4.0 (non-commercial) | present | — |

**Measurements on a cloud Linux CPU** (4 logical CPUs, not a phone): e5-small — 10 ms per text of 32 tokens; multilingual CLIP text — 13 ms; the CLIP vision part — 37 ms per 224×224 picture. Brute force in memory: 10 000 vectors of 512 — about 8 ms per query; 50 000 — 8–13 ms. Search quality was not measured (no labeled set).

**Storage**: `sqlite-vec` on NuGet is only alpha (`0.1.7-alpha.2`); the package exists on PyPI. For SQLite with BLOBs and brute force in memory the measurements give acceptable time up to 50 000 vectors of 512 dimensions (about 100 MB in memory for float32).

## Options (comparison)

| Criterion | A. Text at a provider (OpenRouter, bge-m3 or e5) | B. Images at a multimodal provider (gemini-embedding-2, voyage-multimodal-3.5, titan) | C. Local ONNX (e5-small + CLIP B/32 + CLIP text) | D. Through a picture description (Img2Text, 019) and text search |
|---|---|---|---|---|
| Where vectors are computed | the provider, with the user's key | the provider, with the user's key | on the device | the description — the assistant model; text vectors — as in A |
| Finding pictures without the word in the prompt | no | yes (shared space) | yes (CLIP) | yes, if the description is complete |
| RU / EN quality | multilingual models (by descriptions); not measured | not measured | not measured | depends on the description; not measured |
| Price | $0.01–0.02 per 1M text tokens; images — see the table above | text $0.12–0.20 per 1M; image — the unit is unclear (see above) | 0 per query; disk space ≈ 860 MB (e5 O4 + CLIP vision + CLIP text) | the price of describing every picture (019, unknown in image tokens) |
| Size in the build | 0 | 0 | ≈ 860 MB of weights (unacceptable for a mobile build; download on demand is a separate task) | 0 |
| Android / iOS | works (HTTP) | works (HTTP) | ONNX Runtime on mobile not verified | works |
| Privacy | text goes to the provider (as prompts do now) | text and images go to the provider — a new data flow, consent needed | nothing leaves | the picture goes to the description model (already so in 019) |
| Model license | the provider | the provider | MIT / Apache-2.0 for the chosen ones; CLIP metadata unclear | the provider |
| Offline work | no | no | yes | no |

## Decision
1. **Text** — embeddings through OpenRouter, the `baai/bge-m3` model. The search indexes session titles, prompts and text descriptions of pictures.
2. **Pictures** — pixels, image URLs and files are not sent to the embedding provider. Search by picture content uses the text description already created (option D), if there is one.
3. **Vector storage**: SQLite, a table with BLOBs (float32 or float16), brute force in memory. `sqlite-vec` is not used until there is a stable version for .NET.
4. **There will be no offline mode** (the owner's decision of 2026-10-29). Local ONNX models, weights, a model downloader and a fallback without a network are not supported. Semantic indexing/queries need the network, an OpenRouter key and an available `POST /embeddings`; the ordinary fuzzy search of 079 stays local.
5. **Reindexing**: new records are indexed when saved. The strategy for old data remains an open implementation question: at once when turned on or gradually in the background.

## Open questions
1. Reindexing old sessions: at once on the first enabling or gradually in the background?
2. The quality of `baai/bge-m3` on the app's data is not measured: a labeled RU/EN query set is needed before judging quality, but this does not bring back offline mode.

## Consequences
- Semantic search does not add ONNX Runtime, model weights or mobile runtime checks.
- The feature depends on the network, the OpenRouter key and the availability of the embeddings API; the local fuzzy search keeps working separately.
- Pixels do not create a new data flow to the embedding provider; only texts already allowed for OpenRouter are used.
- Quality must be measured on a labeled set; the result may affect the model, but not the decision against offline mode.

### Implementation refinement, 2026-10-31 (the owner's decisions) and 2026-11-02
- When turned on, the whole history is indexed; Meaning is a separate mode, its results are not merged with fuzzy search.
- Before confirmation, the number of texts and a warning about paid requests are shown. The index is limited to the 10 000 latest texts and stores float32 little-endian BLOBs in SQLite; a hash of the normalized text serves as the source version.
- Turning the mode off deletes the vectors. Queries shorter than three characters are skipped, input is debounced by 400 ms, query vectors are cached in an LRU of 50 queries.
- The dimension of `baai/bge-m3` and the `usage.cost` unit for embeddings were not measured by a live paid request; the code does not treat `usage.cost` as USD. Peak memory was not measured locally; nominally 10 000 vectors take about 39 MiB (41 MB) at 1024 float32 per vector, if the real dimension is confirmed.
