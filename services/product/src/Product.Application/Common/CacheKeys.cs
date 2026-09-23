namespace Product.Application.Common;

public static class CacheKeys
{
    public const string ProductPrefix = "product:";
    public const string CategoryPrefix = "category:";

    public static string Product(Guid id) => $"{ProductPrefix}id:{id}";
    public static string ProductSearch(string hash) => $"{ProductPrefix}search:{hash}";
    public static string CategoryList(bool onlyActive) => $"{CategoryPrefix}list:{onlyActive}";
}
