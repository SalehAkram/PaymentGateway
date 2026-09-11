using PaymentGateway.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddPaymentGatewayControllers();
builder.Services.AddExceptionHandler<PaymentGateway.Api.Errors.ApiExceptionHandler>();
builder.Services.AddProblemDetails();
// The custom handler records safe error metadata; avoid duplicate framework exception dumps.
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssemblyContaining<PaymentGateway.Api.Commands.ProcessPaymentCommand>());

builder.Services.AddSingleton<PaymentGateway.Core.Abstractions.IPaymentsRepository,
    PaymentGateway.Infra.Persistence.PaymentsRepository>();

builder.Services.AddHttpClient<PaymentGateway.Core.Abstractions.IAcquiringBankClient,
    PaymentGateway.Infra.Banking.AcquiringBankClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AcquiringBank:BaseUrl"]
        ?? throw new InvalidOperationException("AcquiringBank:BaseUrl is required."));
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int>("AcquiringBank:TimeoutSeconds", 10));
});

var app = builder.Build();
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
