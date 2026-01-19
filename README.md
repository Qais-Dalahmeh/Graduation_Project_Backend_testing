# Graduation Project - Backend (Loyalty & Rewards System) 🚀

This is the backend system for a Graduation Project focused on a **Loyalty and Rewards Management System**. The project is built using **ASP.NET Core** and features a robust testing architecture covering both Unit and Integration tests.

## 🛠 Tech Stack
* **Framework:** .NET 8 / ASP.NET Core Web API
* **Database:** Entity Framework Core (SQL Server / In-Memory for Testing)
* **Testing:** xUnit, FluentAssertions, WebApplicationFactory
* **Documentation:** Swagger UI (OpenAPI)

## 🏗 Project Architecture
The project follows a **Service-Pattern Architecture** to ensure clean separation of concerns:
* **Controllers:** Handling API endpoints and HTTP requests.
* **Services:** Containing the core business logic (Points calculation, Coupon redemption, etc.).
* **Data Layer:** EF Core DbContext with Fluent API configurations.

## 🧪 Testing Strategy (The Strong Point)
The project stands out with its high code coverage and automated testing suites:

### 1. Unit Testing
Located in the `UnitTesting` directory. These tests verify the internal logic of services in isolation.
* **Feature Coverage:** Points calculation, Phone number normalization, Store name trimming, and Security checks.
* **Mocking:** Using In-Memory databases to ensure fast and isolated test runs.

### 2. Integration Testing
Located in the `IntegrationTesting` directory. These tests verify the interaction between the API, Database, and Middleware.
* **Custom Web Application Factory:** Used to bootstrap the API in memory for testing.
* **Scenarios:** Full User Registration flow, API Validation (400 Bad Request handling), and DB Default constraints.



## 🚀 How to Run the Tests
To execute the test suite, run the following command in the terminal:

```bash
dotnet test
