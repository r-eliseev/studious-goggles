# 081: пробный код поиска по смыслу (замеры скорости и размера)

Проект `SpeedProbe` — консольная программа .NET 10 с ONNX Runtime (`Microsoft.ML.OnnxRuntime` 1.31.1). Она не входит в `ImageGen/` и не компилируется вместе с ним. Замеры сделаны 2026-10-29 в облачной Linux-среде (4 логических CPU, без GPU). Веса моделей не хранятся в репозитории: их скачивают в `tmp/models/` (папка игнорируется git).

## Запуск
```
# модели: https://huggingface.co/intfloat/multilingual-e5-small/resolve/main/onnx/model_O4.onnx
#         https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model.onnx
#         https://huggingface.co/sentence-transformers/clip-ViT-B-32-multilingual-v1/resolve/main/onnx/model_O4.onnx
cd SpeedProbe
dotnet build -c Release
dotnet bin/Release/net10.0/SpeedProbe.dll <model.onnx> text 100 32     # текстовая модель, 32 токена
dotnet bin/Release/net10.0/SpeedProbe.dll <model.onnx> image 100       # визуальная модель, 1×3×224×224
dotnet bin/Release/net10.0/SpeedProbe.dll scan 50000 512               # перебор векторов в памяти
```
Входы синтетические (случайные id токенов и пиксели): замер показывает скорость и размер, но не качество.

## Результаты

| Что | Размер файла | Среднее время одного прохода | Размер вектора |
|---|---|---|---|
| multilingual-e5-small, `onnx/model_O4.onnx`, текст 32 токена | 224 МБ (235 МБ по HF) | 10.0 мс | 384 (после mean pooling; замер выдал выход целиком: 32×384) |
| CLIP B/32 мультиязычный текстовый энкодер, `onnx/model_O4.onnx`, текст 32 токена | 257 МБ (269 МБ по HF) | 12.8 мс | 512 (замер выдал выход целиком: 32×768, проекция не замерялась) |
| CLIP ViT-B/32, визуальная часть `vision_model.onnx`, 224×224 | 335 МБ (352 МБ по HF) | 36.7 мс | 512 |

Перебор косинусного сходства в памяти (float32, 512 измерений, поиск лучшего из N, 30 запросов):

| N векторов | Память | Время одного запроса (среднее / медиана) |
|---|---|---|
| 10 000 | 19.5 МБ | 7.4 / 8.3 мс |
| 50 000 | 97.7 МБ | 13.2 / 7.9 мс |

## Что не измерено
- **Качество поиска** (русский и английский, картинки): нет размеченного набора запросов и картинок в репозитории, ключей для онлайн-проверки в среде нет. Нужен следующий PoC с набором из 20 запросов и 50 картинок.
- **Квантованные варианты** (`vision_model_quantized.onnx` 89 МБ, `model_qint8_avx512_vnni.onnx` 118 МБ для e5-small — последний собран под конкретный набор инструкций CPU): не замерялись.
- **Android и iOS**: скорость и поддержка ONNX Runtime в мобильных сборках MAUI не проверялись (нет устройств и эмулятора; `docs/knowledge/claude-cloud-session.md`).
- **sqlite-vec**: в NuGet есть только alpha-версия `0.1.7-alpha.2`; в PyPI пакет есть. В .NET не проверялся.
