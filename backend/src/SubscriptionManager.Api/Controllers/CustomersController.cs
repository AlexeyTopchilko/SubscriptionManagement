using Microsoft.AspNetCore.Mvc;
using SubscriptionManager.Api.DTOs;
using SubscriptionManager.Api.Services;

namespace SubscriptionManager.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;

    public CustomersController(ICustomerService service)
    {
        _service = service;
    }

    /// <summary>Get the list of all customers</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var customers = await _service.GetAllAsync();
        return Ok(customers);
    }

    /// <summary>Get a customer by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var customer = await _service.GetByIdAsync(id);
        return customer is null ? NotFound() : Ok(customer);
    }

    /// <summary>Create a new customer</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return result.ResultType switch
        {
            ServiceResultType.Ok => CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data),
            ServiceResultType.Conflict => Conflict(new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }

    /// <summary>Update customer data</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result.ResultType switch
        {
            ServiceResultType.Ok => Ok(result.Data),
            ServiceResultType.NotFound => NotFound(new { message = result.ErrorMessage }),
            ServiceResultType.Conflict => Conflict(new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }

    /// <summary>Delete a customer</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _service.DeleteAsync(id);
        return result.ResultType switch
        {
            ServiceResultType.Ok => NoContent(),
            _ => NotFound(new { message = result.ErrorMessage })
        };
    }
}
