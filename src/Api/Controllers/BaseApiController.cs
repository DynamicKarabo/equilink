using Microsoft.AspNetCore.Mvc;

namespace EquiLink.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
}
