using BuildingBlocks.Infrastructure.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.Application.Categories;
using Product.Application.Categories.Commands;
using Product.Application.Categories.Queries;

namespace Product.Api.Controllers;

[Route("api/categories")]
[Produces("application/json")]
public sealed class CategoriesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = false, CancellationToken ct = default)
        => ToResponse(await Dispatcher.Ask(new GetCategoriesQuery(onlyActive), ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResponse(await Dispatcher.Ask(new GetCategoryByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryCommand command, CancellationToken ct)
    {
        var result = await Dispatcher.Send(command, ct);
        return result.IsSuccess
            ? CreatedResponse(result, nameof(GetById), new { id = result.Value.Id })
            : ToResponse(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(
            new UpdateCategoryCommand(id, request.Name, request.Description, request.IsActive), ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResponse(await Dispatcher.Send(new DeleteCategoryCommand(id), ct));
}

public sealed record UpdateCategoryRequest(string Name, string? Description, bool IsActive);
