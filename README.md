You’ve set up a multi-tenant SaaS app following Microsoft’s [SaaS monetization guidance](https://learn.microsoft.com/en-us/azure/marketplace/partner-center-portal/azure-apps-saas-registration). You have:

* A **multi-tenant backend** Azure AD app registration that:

  * Exposes scopes
  * Calls Microsoft SaaS Fulfillment APIs using the publisher token

* A **multi-tenant frontend** Azure AD app registration that:

  * Uses MSAL for login
  * Requests delegated permissions, including backend scopes

When an **external user** signs into the frontend:

* They’re redirected to the expected Microsoft login page
* But instead of seeing the consent prompt, they are stuck in a **redirect loop** between:

  * `login.microsoftonline.com/organizations/oauth2/v2.0/authorize`
  * `login.microsoftonline.com/common/reprocess`
  * frontend home page (which redirects back to login)

No **Enterprise Application** is created in the client tenant.
 Root Cause of the Infinite Redirect Loop

This is commonly caused by one or more of the following:

. **Missing Consent**

The user may not be completing the consent prompt (or not seeing it), resulting in a failed token acquisition.

* Without a valid access token, your frontend tries to reinitiate auth, causing a loop.
* If you're using **MSAL.js**, ensure the `acquireTokenSilent()` fallback to `redirect` or `popup` is properly implemented.

[MSAL Silent Token Acquisition](https://learn.microsoft.com/en-us/entra/msal/js/token-acquisition-silent)

 **Misconfigured Redirect URI**

The URI in your request:

```
https://login.microsoftonline.com/organizations/oauth2/v2.0/authorize
```

must include a `redirect_uri` parameter that **exactly matches** what's registered in Azure for the frontend app.

Even a mismatch in trailing slashes or `https` casing can cause token failures.

[Register Redirect URIs](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-redirect-uri)

**Scopes Not Granted Properly**

You’re using a scope like:

```
scope=api://{backend-app-id}/{scope-name}
```

This is valid, **but the backend must expose** that scope under:

> App registration → Expose an API → Application ID URI → Scopes

And the frontend must include those scopes under:

> App registration → API permissions → “My APIs” → \[Backend App] → Delegated Permissions

If the frontend app is missing **API permissions** for backend scopes, the user will never see a combined consent screen and token issuance will fail.

[Expose and consume APIs securely](https://learn.microsoft.com/en-us/azure/active-directory/develop/quickstart-configure-app-expose-web-apis)

 **Missing Token Handling Logic in Frontend**

After a successful login and consent, if your frontend doesn't store or read the acquired token (ID or access), MSAL will retry the auth flow, causing another redirect.

Ensure your app:

* Caches the token properly (use `sessionStorage` or `localStorage`)
* Verifies the auth state (e.g., `msalInstance.getAllAccounts()` or similar)

[Best practices for MSAL](https://learn.microsoft.com/en-us/entra/msal/js/best-practices)

---

 **What causes the infinite redirect loop? What’s the correct configuration?**

**Causes:**

* User doesn’t complete consent → no token issued
* Redirect URI mismatch
* No token caching → MSAL redirects again
* Backend scope not properly exposed or added to frontend

**Fix:**

* Double-check scope and consent setup
* Validate that `redirect_uri` is registered and passed properly
* Ensure frontend handles and caches token


 **Does adding backend scopes to frontend’s API permissions suffice?**
 **Yes.** That’s the correct way to allow your frontend to request consent for backend scopes in a multi-tenant flow.

Steps:

1. Backend app:

   * Go to *Expose an API*
   * Define scope: `user_impersonation` or custom
2. Frontend app:

   * Go to *API permissions* → *Add a permission* → *My APIs* → Select backend app → Choose scopes
3. Consent prompt will now include backend scopes

 **Is your architecture conceptually correct?**

Yes.

| Component | Multi-Tenant? | Enterprise App Expected?      | Notes                                |
| --------- | ------------- | ----------------------------- | ------------------------------------ |
| Frontend  | Yes           |  Yes, in client tenant       | Required for login                   |
| Backend   | Yes           |  No (unless directly called) | Only frontend creates Enterprise App |

Note: If the frontend app is the **only one initiating auth flows**, then **backend does not create its own Enterprise App** in external tenants.



Use [Microsoft's OAuth 2.0 error codes](https://learn.microsoft.com/en-us/entra/identity-platform/reference-error-codes) or monitor the browser dev console for **AADSTS** errors during the redirect to pinpoint what's failing in the flow.



You can also force admin consent for testing:

```
https://login.microsoftonline.com/{external-tenant-id}/adminconsent?client_id={frontend-app-id}
```



Answered using Microsoft Docs: [OAuth2 Auth Code Flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow), [App registration best practices](https://learn.microsoft.com/en-us/azure/active-directory/develop/howto-convert-app-to-be-multi-tenant), and [MSAL token handling](https://learn.microsoft.com/en-us/entra/msal/overview).

let know if helped will post this as answer so it helps others . So  you accept below answer by following  [link](https://meta.stackexchange.com/questions/5234/how-does-accepting-an-answer-work/5235#5235) to reach others as well
