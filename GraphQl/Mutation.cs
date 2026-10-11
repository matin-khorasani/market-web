using System.ComponentModel.DataAnnotations;
using HotChocolate;
using HotChocolate.Authorization;

public class Mutation
{
    [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
    public async Task<ProductDto> CreateProduct(
        ProductSaveDto input,
        [Service] IProductService products)
    {
        Validate(input);

        var (product, error) = await products.CreateAsync(input);
        if (error is not null) throw Fail(error, "BAD_REQUEST");
        return product!;
    }

    [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
    public async Task<ProductDto> UpdateProduct(
        int id,
        ProductSaveDto input,
        [Service] IProductService products)
    {
        Validate(input);

        var (found, error) = await products.UpdateAsync(id, input);
        if (!found) throw Fail("Product not found.", "NOT_FOUND");
        if (error is not null) throw Fail(error, "BAD_REQUEST");

        return (await products.GetAsync(id))!;   // کش قبلاً باطل شده، پس داده‌ی تازه میاد
    }

    [Authorize(Roles = new[] { nameof(UserRole.Admin) })]
    public async Task<bool> DeleteProduct(int id, [Service] IProductService products)
    {
        var (found, error) = await products.DeleteAsync(id);
        if (!found) throw Fail("Product not found.", "NOT_FOUND");
        if (error is not null) throw Fail(error, "BAD_REQUEST");
        return true;
    }

    // ---- کمکی‌ها ----

    // همون قوانین [Range]/[Required]/[StringLength] رو که REST خودکار اجرا می‌کنه، اینجا دستی اجرا می‌کنیم
    private static void Validate(ProductSaveDto input)
    {
        var results = new List<ValidationResult>();
        // validateAllProperties: true حیاتیه؛ بدونش فقط [Required] چک می‌شه
        if (!Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true))
            throw Fail(string.Join("; ", results.Select(r => r.ErrorMessage)), "VALIDATION_ERROR");
    }

    private static GraphQLException Fail(string message, string code)
        => new(ErrorBuilder.New().SetMessage(message).SetCode(code).Build());
}