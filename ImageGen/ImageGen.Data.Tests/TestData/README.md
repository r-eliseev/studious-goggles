# Image metadata test fixtures

- `dimensions.jpg`, `dimensions.png`, and `dimensions.gif` are copies of `simple.jpg`, `mspaint-8x10.png`, and `mspaint-10x10.gif` from [MetadataExtractor .NET test data](https://github.com/drewnoakes/metadata-extractor-dotnet/tree/main/MetadataExtractor.Tests/Data). The source repository is Apache-2.0 licensed; see its [LICENSE](https://github.com/drewnoakes/metadata-extractor-dotnet/blob/main/LICENSE).
- `dimensions.webp` is a copy of `alpha_no_compression.webp` from the [libwebp test data mirror](https://github.com/webmproject/libwebp-test-data). The corresponding [libwebp project COPYING](https://github.com/webmproject/libwebp/blob/main/COPYING) contains its BSD-style redistribution notice.

These files are test-only inputs. Expected dimensions are read from file headers, not inferred from names or extensions.
