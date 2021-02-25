# Authentication & Authorization sample

This sample shows how the YARP proxy can be integrated with the ASP.NET [authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication) and [authorization](https://docs.microsoft.com/en-us/aspnet/core/security/authorization/introduction) system to specify claims requirements on routes that will be enforced by the proxy before it will forward applicable requests. 

The sample includes the following parts:

- ### [Startup.cs](Startup.cs)
  Sets up the ASP.NET server to have the proxy together with the other middleware for authentication, authorization and Razor pages.
  It sets up a custom authorization policy "myPolicy" with a custom claim.

- ### [AccountController.cs](Controllers/AccountController.cs)
  Handles the login UI actions, and adds a value from a field in the login page to the "myCustomClaim" claim in the active identity. That claim is later required by the "myPolicy" authorization policy created in Startup.cs

- ### [appsettings.json](appsettings.json)
  Defines the routes used by the reverse proxy including:
  - /default - requires authentication to access
  - /claim - uses the "myPolicy" authorization policy which requires authentication and a myCustomClaim value of "green"
  - \* - which uses an empty fallback policy so no authentication is required

- ### Login UI
  The Razor pages in [Views/Account](Views/Account) provide the UI to login, logout and be shown when access is denied.

## Usage
Start the sample with ```dotnet run``` which by default will bind to http://localhost:5000 and https://localhost:5001. Try accessing the urls "/", "/default" and "/claim". When shown the login ui, pick a value for myCustomClaim. Using "green" will allow access to content under "/claim", using other values will prevent access.

