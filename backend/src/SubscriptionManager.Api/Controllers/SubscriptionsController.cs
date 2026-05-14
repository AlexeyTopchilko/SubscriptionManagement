using Microsoft.AspNetCore.Mvc;
using SubscriptionManager.Api.DTOs;
using SubscriptionManager.Api.Models;
using SubscriptionManager.Api.Services;

namespace SubscriptionManager.Api.Controllers;

[ApiController]
[Route("api/customers/{customerId:guid}/subscriptions")]
[Produces("application/json")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _service;

    public SubscriptionsController(ISubscriptionService service)
    {
        _service = service;
    }

    /// <summary>Get all subscriptions for a customer</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SubscriptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid customerId)
    {
        var subscriptions = await _service.GetByCustomerIdAsync(customerId);
        return Ok(subscriptions);
    }

    /// <summary>Get a subscription by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid customerId, Guid id)
    {
        var subscription = await _service.GetByIdAsync(customerId, id);
        return subscription is null ? NotFound() : Ok(subscription);
    }

    /// <summary>Create a subscription for a customer</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid customerId, [FromBody] CreateSubscriptionDto dto)
    {
        var result = await _service.CreateAsync(customerId, dto);
        return result.ResultType switch
        {
            ServiceResultType.Ok => CreatedAtAction(nameof(GetById),
                new { customerId, id = result.Data!.Id }, result.Data),
            ServiceResultType.NotFound => NotFound(new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }

    /// <summary>Update a subscription</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid customerId, Guid id, [FromBody] UpdateSubscriptionDto dto)
    {
        var result = await _service.UpdateAsync(customerId, id, dto);
        return result.ResultType switch
        {
            ServiceResultType.Ok => Ok(result.Data),
            ServiceResultType.NotFound => NotFound(new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }

    /// <summary>Change subscription status</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid customerId, Guid id, [FromBody] UpdateSubscriptionStatusDto dto)
    {
        var result = await _service.UpdateStatusAsync(customerId, id, dto.Status);
        return result.ResultType switch
        {
            ServiceResultType.Ok => Ok(result.Data),
            ServiceResultType.NotFound => NotFound(new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }

    /// <summary>Delete a subscription</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid customerId, Guid id)
    {
        var result = await _service.DeleteAsync(customerId, id);
        return result.ResultType switch
        {
            ServiceResultType.Ok => NoContent(),
            _ => NotFound(new { message = result.ErrorMessage })
        };
    }
}
