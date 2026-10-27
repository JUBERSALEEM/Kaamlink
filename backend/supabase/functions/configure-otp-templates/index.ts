const PROJECT_REF = "zyklrndmmcmsktagkzek";
const MANAGEMENT_API_URL = "https://api.supabase.com/v1/projects/" + PROJECT_REF + "/config/auth";

const MAGIC_LINK_CONTENT = [
  '<div style="font-family: system-ui, -apple-system, BlinkMacSystemFont, \'Segoe UI\', sans-serif; max-width: 520px; margin: 0 auto; padding: 32px; border-radius: 28px; background: #f7f5ff; color: #241b40;">',
  '  <h2 style="margin: 0 0 12px; font-size: 24px;">Your KaamLink code</h2>',
  '  <p style="margin: 0 0 20px; color: #5f5775; line-height: 1.6;">Enter this one-time code in the KaamLink app to continue.</p>',
  '  <p style="margin: 0 0 24px; font-size: 32px; font-weight: 800; letter-spacing: 8px; color: #4b2fc9;">{{ .Token }}</p>',
  '  <p style="margin: 0; font-size: 13px; color: #7a7292;">This code expires shortly. If you did not request it, you can ignore this email.</p>',
  "</div>",
].join("\n");

const CONFIRMATION_CONTENT = [
  '<div style="font-family: system-ui, -apple-system, BlinkMacSystemFont, \'Segoe UI\', sans-serif; max-width: 520px; margin: 0 auto; padding: 32px; border-radius: 28px; background: #f7f5ff; color: #241b40;">',
  '  <h2 style="margin: 0 0 12px; font-size: 24px;">Confirm your KaamLink email</h2>',
  '  <p style="margin: 0 0 20px; color: #5f5775; line-height: 1.6;">Enter this one-time code in the KaamLink app to verify your email.</p>',
  '  <p style="margin: 0 0 24px; font-size: 32px; font-weight: 800; letter-spacing: 8px; color: #4b2fc9;">{{ .Token }}</p>',
  '  <p style="margin: 0; font-size: 13px; color: #7a7292;">If you did not request this code, you can ignore this email.</p>',
  "</div>",
].join("\n");

function json(payload: unknown, status: number, headers: Record<string, string>): Response {
  return new Response(JSON.stringify(payload), {
    status,
    headers: {
      ...headers,
      "content-type": "application/json; charset=utf-8",
    },
  });
}

Deno.serve(async (req: Request) => {
  const corsHeaders: Record<string, string> = {
    "access-control-allow-origin": "*",
    "access-control-allow-headers": "authorization, x-admin-token, content-type",
    "access-control-allow-methods": "POST, OPTIONS",
  };

  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  if (req.method !== "POST") {
    return json({ error: "Method not allowed" }, 405, corsHeaders);
  }

  const adminToken = Deno.env.get("OTP_TEMPLATE_ADMIN_TOKEN");
  const providedToken = req.headers.get("x-admin-token");
  if (!adminToken || !providedToken || providedToken !== adminToken) {
    return json({ error: "Unauthorized" }, 401, corsHeaders);
  }

  const managementToken = Deno.env.get("SUPABASE_ACCESS_TOKEN");
  if (!managementToken) {
    return json({
      error: "SUPABASE_ACCESS_TOKEN is not set",
      help: "Create a scoped Management API access token with auth:write, auth_config_write and project_admin_write scopes, then set it as an Edge Function secret named SUPABASE_ACCESS_TOKEN.",
    }, 503, corsHeaders);
  }

  const config = {
    mailer_templates_magic_link_content: MAGIC_LINK_CONTENT,
    mailer_templates_confirmation_content: CONFIRMATION_CONTENT,
    mailer_otp_length: 8,
    password_min_length: 8,
  };

  const response = await fetch(MANAGEMENT_API_URL, {
    method: "PATCH",
    headers: {
      "authorization": "Bearer " + managementToken,
      "content-type": "application/json",
    },
    body: JSON.stringify(config),
  });

  const responseText = await response.text();
  if (!response.ok) {
    return json({
      error: "Management API request failed",
      status: response.status,
      details: responseText,
    }, 502, corsHeaders);
  }

  return json({
    success: true,
    project_ref: PROJECT_REF,
    updated: Object.keys(config),
  }, 200, corsHeaders);
});
