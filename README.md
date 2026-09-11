# Payment Gateway

A .NET 8 API for the Checkout.com payment gateway challenge. It validates payment requests, sends valid requests to the supplied acquiring bank simulator, stores the result in memory, and lets a merchant retrieve a payment by its ID.

The implementation follows the [challenge requirements](https://github.com/cko-recruitment/#requirements). The supplied simulator configuration and `.editorconfig` are unchanged.

## Running locally

You need the .NET 8 SDK and Docker Desktop with its Linux engine running. Run the commands below from the solution root, where `PaymentGateway.sln` and `docker-compose.yml` are located.

### 1. Start the bank simulator

```powershell
docker compose up -d
docker compose ps
```

The bank endpoint is `http://localhost:8080/payments`. Mountebank's management interface is at `http://localhost:2525`.

To stop the Docker simulator when finished:

```powershell
docker compose down
```

### 2. Build and start the API

```powershell
dotnet restore PaymentGateway.sln
dotnet build PaymentGateway.sln --no-restore
dotnet run --project src/PaymentGateway.Api/PaymentGateway.Api.csproj --launch-profile PaymentGateway.Api
```

Open [Swagger](https://localhost:7092/swagger). Alternatively, open the solution in Visual Studio, select `PaymentGateway.Api` as the startup project, and run it with the `PaymentGateway.Api` launch profile.

If the development HTTPS certificate is missing or untrusted, run `dotnet dev-certs https --trust`. For a local test using HTTP instead:

```powershell
dotnet run --project src/PaymentGateway.Api/PaymentGateway.Api.csproj --launch-profile PaymentGateway.Api -- --urls http://localhost:5067
```

Then open [HTTP Swagger](http://localhost:5067/swagger). Swagger is enabled in the Development environment. Use synthetic card details for local testing.

### Alternative simulator startup

Docker image downloads were blocked by a local connectivity issue during development. The same simulator was run directly with Node.js and npm, using Mountebank 2.8.1 and the original configuration:

```powershell
npx --yes --package mountebank@2.8.1 mb --configfile imposters/bank_simulator.ejs --allowInjection --localOnly --nologfile
```

Keep that terminal open; Ctrl+C stops it. The first run downloads the package into npm's cache. This uses the same ports as Docker, so run only one simulator at a time. `--localOnly` restricts access to the local machine; `--allowInjection` is needed for the supplied authorization-code behavior.

### Configuration

The API reads the following settings from `src/PaymentGateway.Api/appsettings.json`:

```json
"AcquiringBank": {
  "BaseUrl": "http://localhost:8080/",
  "TimeoutSeconds": 10
}
```

They can be overridden with environment variables `AcquiringBank__BaseUrl` and `AcquiringBank__TimeoutSeconds`. The supplied Compose file hosts the simulator only; the API runs separately. If the API is hosted elsewhere, the bank URL must be reachable from that environment rather than assuming `localhost` refers to the simulator.

## Using the API

### Process a payment

`POST /api/v1/payments`

```json
{
  "cardNumber": "2222405343240007",
  "expiryMonth": 4,
  "expiryYear": 2030,
  "currency": "GBP",
  "amount": 1050,
  "cvv": "012"
}
```

Use a future expiry date when running these examples. Amount is in minor currency units: `1050` means GBP 10.50.

An example HTTP 200 response:

```json
{
  "id": "cfd7b12b-a8fb-41c8-82b3-77ed647681a1",
  "status": "Authorized",
  "cardNumberLastFour": "0007",
  "expiryMonth": 4,
  "expiryYear": 2030,
  "currency": "GBP",
  "amount": 1050
}
```

Both Authorized and Declined return HTTP 200: the bank has made a decision and the gateway has saved the result. A decline is an expected payment outcome, not an application failure.

Invalid input returns HTTP 400 with field errors and an explicit Rejected outcome. For example:

```json
{
  "title": "Payment request rejected.",
  "status": 400,
  "errors": {
    "CardNumber": ["Card number must contain 14 to 19 digits."]
  },
  "paymentStatus": "Rejected"
}
```

Rejected requests do not reach the command handler or bank, and no payment is stored. `paymentStatus` is separate from the numeric HTTP `status` field in Problem Details.

### Retrieve a payment

`GET /api/v1/payments/{id}`

Use the ID returned by POST. A stored payment returns HTTP 200 with the same fields shown above. An unknown ID returns HTTP 404. The route requires a GUID, so malformed IDs also return 404.

Keep the API running between POST and GET. Restarting it clears the in-memory store.

### Bank failures

| Situation | HTTP response |
| --- | --- |
| Bank unavailable or connection failure | 503 |
| Bank timeout | 504 |
| Invalid bank response or unexpected bank HTTP error | 502 |
| Unexpected internal failure | 500 |

These responses use Problem Details with a safe title and a trace ID. They do not expose raw bank responses, stack traces, or internal exception messages. Caller cancellation is kept separate from a bank timeout.

## Design choices

### Project boundaries

I used a small Clean Architecture structure to separate HTTP concerns, domain data, and external integrations:

| Project | Responsibility |
| --- | --- |
| `src/PaymentGateway.Api` | Controllers, request/response DTOs, MediatR commands and queries, handlers, validation, error responses, and dependency registration |
| `PaymentGateway.Core` | `PaymentEntity`, repository and bank interfaces, bank contract models, and bank failure types |
| `PaymentGateway.Infra` | Bank HTTP client, bank JSON DTOs, and in-memory repository |
| `test/PaymentGateway.Api.Tests` | Automated tests |

Core has no dependency on API, Infra, or MediatR. Commands and handlers live in API as a deliberate compromise to keep three production projects. A separate Application project could hold this orchestration if the application grew.

The persisted `PaymentEntity` is separate from the public `PaymentResponseDto`. POST and GET share that response DTO because the required response fields are identical. Bank-specific JSON names and expiry formatting stay in Infra.

Changing storage should leave the processing flow unchanged as long as the repository contract still fits. 

### MediatR

Each operation has a focused handler with only the dependencies it needs. The controller maps HTTP input to a command or query and maps the result back to a response. A service class would also work; MediatR adds indirection and a package dependency, but provides a consistent way to organise these operations.

### Validation

I considered a custom validation rule runner. The rules here are small and fixed, so built-in Data Annotations were enough without introducing separate rule classes and registration.

Attributes handle required fields, lengths, allowed digits, and ranges. `IValidatableObject` checks expiry month and year together. A `TimeProvider` allows tests to supply a fixed date; the application uses the system UTC clock by default.

Card numbers and CVVs are strings because they are not quantities and leading zeros matter. Nullable numeric request fields allow missing values to be distinguished from zero.

Assumptions and limits:

- Supported currencies are GBP, USD, and EUR, supplied in uppercase.
- Cards remain valid through their expiry month, using UTC.
- Expiry years are limited to 1–9999.
- Amount uses a C# `int`. No positive-minimum rule was added because the brief only specifies a required integer. Zero and negative values therefore pass local validation.
- No Luhn check or removal of spaces/separators was added; card numbers must contain 14–19 ASCII digits exactly.

### Versioning

Routes include `/api/v1`. This leaves room for a future breaking contract while retaining v1 for existing clients. The v2 controller is an empty template and exposes no endpoints.

### Storage and HTTP integration

The repository is a singleton backed by a private `ConcurrentDictionary`. It supports concurrent access and rejects duplicate IDs instead of overwriting a payment. It stores only the safe payment entity, including the last four card digits.

The bank client is registered with `AddHttpClient`. `IHttpClientFactory` manages the underlying HTTP handler lifetimes and connection pools. The URL and timeout are configurable, and cancellation tokens are passed through. There are no automatic payment retries.

### Logging and errors

Structured logs record processing outcomes and elapsed time. Bank-call logs include last four digits, currency, minor-unit amount, HTTP outcome, and duration. They do not log the full card number, CVV, expiry details, or raw payloads.

Logging is kept directly in the handlers/client for now. Shared timing behavior could move into a MediatR pipeline if repetition grows. Azure Application Insights and distributed tracing are not configured in this submission.

The bank client translates known failures into a bank-specific exception. A central .NET exception handler maps failures to HTTP responses and logs safe metadata with a trace ID. Duplicate exception-handler middleware logging is suppressed. This keeps HTTP status decisions out of the bank client and avoids treating a failure as a decline.

## Tests

Run the automated suite from the solution root:

```powershell
dotnet test PaymentGateway.sln
```

The tests do not require Docker or a running simulator. They cover:

- Request validation, including missing fields, format boundaries, and expiry dates.
- Handler authorization/decline behavior, bank request mapping, and failures.
- Repository save/retrieve behavior and duplicate IDs.
- HTTP routing, validation, command/query mapping, safe response fields, and GET 404.
- Central error response mappings.

Tests use xUnit and Moq. HTTP-level tests use `WebApplicationFactory` with a mocked MediatR sender; they do not exercise the real bank client or the full processing flow.

The following were also checked manually through Swagger against the supplied simulator:

| Card ending | Expected result |
| --- | --- |
| 7 | Authorized |
| 8 | Declined |
| 0 | Bank failure, returned as HTTP 503 |

POST followed by GET was checked manually as well. Automated simulator integration tests are not included. They would be a useful next addition to protect the real HTTP contract, along with timeout/cancellation and malformed-response coverage for the bank client.

## Known limitations and production considerations

### Payment consistency

The bank call and local database write cannot be performed as one atomic transaction. For example, the bank could authorize a payment but the subsequent database write could fail, leaving the gateway without a record of the successful authorization.

A more resilient design would first persist the payment as `Processing` with a stable payment ID, then send that ID to the bank as an idempotency reference. If the bank succeeds but updating the local payment to `Authorized` fails, the gateway can safely leave the payment as `Processing` rather than incorrectly reporting it as declined or authorized.

The gateway could return `202 Accepted` for an unresolved payment and reconcile it asynchronously by querying the bank for the authoritative outcome. The merchant could subsequently retrieve the final status using the payment ID.

The supplied simulator does not provide idempotency or payment-status lookup, so this recovery flow is not implemented.

### Storage
Storage is temporary and local to one API instance. It is not shared across instances and has no retention limit. Durable storage would be needed before scaling across instances. I kept reads and writes in one API; separating them or introducing CQRS would need evidence of a useful benefit rather than traffic assumptions alone.

### Merchant authentication and payment security

This exercise does not implement merchant authentication, payment ownership checks, or production payment-data controls. Those would need addressing before real use. Last-four-only storage and careful logging reduce exposure but do not make this a production-ready or compliance-certified payment gateway.
