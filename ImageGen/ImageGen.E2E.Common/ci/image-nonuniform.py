#!/usr/bin/env python3
"""Measure the share of BMP pixels differing from the channel mean by >16."""

import json
import struct
import sys
from pathlib import Path


def measure(path: Path) -> dict[str, int | float]:
    data = path.read_bytes()
    if len(data) < 54 or data[:2] != b"BM":
        raise ValueError("expected a Windows BMP image")

    pixel_offset = struct.unpack_from("<I", data, 10)[0]
    dib_size = struct.unpack_from("<I", data, 14)[0]
    if dib_size < 40 or len(data) < 14 + dib_size:
        raise ValueError("unsupported BMP DIB header")

    width, signed_height, planes, bits_per_pixel, compression = struct.unpack_from("<iiHHI", data, 18)
    valid_compression = compression == 0 or (compression == 3 and bits_per_pixel == 32)
    if width <= 0 or signed_height == 0 or planes != 1 or bits_per_pixel not in (24, 32) or not valid_compression:
        raise ValueError("expected uncompressed 24-bit or supported 32-bit BMP")

    if compression == 3:
        mask_offset = 14 + 40 if dib_size >= 52 else 14 + dib_size
        if mask_offset + 12 > pixel_offset:
            raise ValueError("BMP bit masks overlap or exceed pixel data")
        red_mask, green_mask, blue_mask = struct.unpack_from("<III", data, mask_offset)
        if (red_mask, green_mask, blue_mask) != (0x00FF0000, 0x0000FF00, 0x000000FF):
            raise ValueError("unsupported 32-bit BMP channel masks")

    height = abs(signed_height)
    bytes_per_pixel = bits_per_pixel // 8
    row_stride = ((width * bits_per_pixel + 31) // 32) * 4
    expected_end = pixel_offset + row_stride * height
    if pixel_offset < 14 + dib_size or expected_end > len(data):
        raise ValueError("BMP pixel data is truncated or malformed")

    totals = [0, 0, 0]
    pixels: list[tuple[int, int, int]] = []
    for row in range(height):
        row_start = pixel_offset + row * row_stride
        for column in range(width):
            offset = row_start + column * bytes_per_pixel
            blue, green, red = data[offset : offset + 3]
            pixel = (blue, green, red)
            pixels.append(pixel)
            for channel, value in enumerate(pixel):
                totals[channel] += value

    count = len(pixels)
    means = [total / count for total in totals]
    different = sum(
        any(abs(channel - mean) > 16 for channel, mean in zip(pixel, means))
        for pixel in pixels
    )
    return {"width": width, "height": height, "nonUniformPixels": different, "pixels": count, "ratio": different / count}


def main() -> int:
    if len(sys.argv) != 2:
        print(f"usage: {sys.argv[0]} <image.bmp>", file=sys.stderr)
        return 2
    result = measure(Path(sys.argv[1]))
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, struct.error) as error:
        print(f"image check failed: {error}", file=sys.stderr)
        raise SystemExit(1)
