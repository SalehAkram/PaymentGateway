using Microsoft.AspNetCore.Mvc;

namespace PaymentGateway.Api.Controllers.V2;

[Route("api/v2/payments")]
[ApiController]
public class PaymentsController : ControllerBase
{
    // Template only: no V2 endpoints are available yet.
    // Add actions here when a breaking API contract change requires a new version.
    // Keep V1 available for existing clients and reuse Core business logic across versions.
    // Introduce V2 request/response models only where the contract differs from V1.
}
