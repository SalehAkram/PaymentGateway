using Microsoft.AspNetCore.Mvc;

namespace PaymentGateway.Api.Extensions;

// Keeps controller setup out of Program.cs and adds Rejected to automatic HTTP 400 validation responses.
public static class ControllerServiceCollectionExtensions
{
    public static IMvcBuilder AddPaymentGatewayControllers(this IServiceCollection services)
    {
        return services.AddControllers().ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problem = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Payment request rejected."
                };
                problem.Extensions["paymentStatus"] = "Rejected";

                var response = new BadRequestObjectResult(problem);
                response.ContentTypes.Add("application/problem+json");
                return response;
            };
        });
    }
}
