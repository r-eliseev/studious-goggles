# 0021. MetadataExtractor for reading image dimensions

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-30

## Context
The generation card needs the actual pixel dimensions of the saved file for PNG, JPEG, WebP and GIF. The dimensions must be read without fully decoding the image, and the Presentation layer must not depend on file formats.

## Decision
Use MetadataExtractor 2.9.3 only in `ImageGen.Data` to read the headers of PNG, JPEG, WebP and GIF. Pin XmpCore 6.1.10.1 as its transitive dependency and ship the Apache-2.0 and BSD 3-Clause notices with both heads. Return the result as `ImageDimensions` through the `IImageFileStore` contract; do not store derived dimensions in the DB.

## Alternatives
- Parsing headers by hand: fewer dependencies, but more of our own format code and a risk of mishandling format variants.
- ImageSharp: not chosen because of its Split License.
- UI decoders: tie metadata reading to the platform and require decoding the image.

## Consequences
Data gets a maintained metadata parsing library; the MetadataExtractor and XmpCore licenses must be shipped with both heads. Metadata I/O runs bounded and in the background. EXIF orientation is not applied: the raster dimensions recorded in the file are shown.
