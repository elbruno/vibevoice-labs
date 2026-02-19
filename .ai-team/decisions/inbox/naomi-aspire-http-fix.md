### Aspire Frontend-to-Backend HTTP Fix

**By:** Naomi (Backend Dev)  
**Date:** 2026-02-19  
**Status:** Implemented

**What:**
- Changed `AppHost.cs` to add `.WithHttpEndpoint(targetPort: 8000, env: "PORT")` on the `AddUvicornApp` call
- Changed `VoiceLabs.Web/Program.cs` HttpClient base address from `https://backend` to `http://backend`

**Why:**
The Python uvicorn backend only serves HTTP (not HTTPS). Aspire service discovery uses the URI scheme to resolve named endpoints — `https://backend` tried to find an HTTPS endpoint that didn't exist. Adding `WithHttpEndpoint` explicitly declares the backend's HTTP endpoint for Aspire, and switching to `http://` ensures the frontend connects on the correct scheme.

**Impact:**
- Blazor frontend can now reach the Python FastAPI backend through Aspire service discovery
- The `PORT` environment variable is passed to uvicorn via the `env: "PORT"` parameter
- Build verified: 0 errors, 0 warnings
