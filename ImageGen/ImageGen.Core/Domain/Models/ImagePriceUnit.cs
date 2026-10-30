namespace ImageGen.Core.Domain.Models;

/// <summary>Unit used by the provider for an image model price. Values are persisted in catalog JSON.</summary>
public enum ImagePriceUnit
{
    Image = 0,
    Megapixel = 1,
    Token = 2,
    Request = 3,
}
