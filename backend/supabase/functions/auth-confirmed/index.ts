const APP_SCHEME = "kaamlink";

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, (char) => {
    switch (char) {
      case "&": return "&amp;";
      case "<": return "&lt;";
      case ">": return "&gt;";
      case '"': return "&quot;";
      case "'": return "&#39;";
      default: return char;
    }
  });
}

Deno.serve(async (req: Request) => {
  const url = new URL(req.url);
  const query = url.searchParams.toString();
  const deepLink = APP_SCHEME + "://auth-confirmed" + (query ? "?" + query : "");
  const safeDeepLink = escapeHtml(deepLink);

  const html = [
    "<!doctype html>",
    '<html lang="en">',
    "<head>",
    '  <meta charset="utf-8" />',
    '  <meta name="viewport" content="width=device-width, initial-scale=1" />',
    "  <title>KaamLink - Email confirmed</title>",
    "  <style>",
    "    :root { color-scheme: light; }",
    "    body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #f4f1ff; font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; color: #221a3a; }",
    "    main { width: min(92vw, 440px); padding: 40px 32px; border-radius: 32px; background: #ffffff; box-shadow: 18px 18px 50px rgba(86, 62, 160, 0.14), -14px -14px 40px rgba(255, 255, 255, 0.9); text-align: center; }",
    "    .mark { width: 64px; height: 64px; margin: 0 auto 22px; border-radius: 22px; display: grid; place-items: center; background: linear-gradient(135deg, #6d4df2, #14c8c1); color: #ffffff; font-size: 30px; font-weight: 800; }",
    "    h1 { margin: 0 0 10px; font-size: 26px; letter-spacing: -0.02em; }",
    "    p { margin: 0 0 20px; line-height: 1.6; color: #5f5775; }",
    "    .button { display: inline-block; padding: 15px 28px; border-radius: 18px; background: linear-gradient(135deg, #6d4df2, #7b5cf5); color: #ffffff; font-weight: 700; text-decoration: none; box-shadow: 0 14px 28px rgba(109, 77, 242, 0.28); }",
    "    .hint { margin-top: 26px; font-size: 13px; color: #7a7292; }",
    "    code { display: inline-block; margin-top: 8px; padding: 8px 12px; border-radius: 12px; background: #f4f1ff; color: #4b3a9c; word-break: break-all; font-size: 12px; }",
    "  </style>",
    "</head>",
    "<body>",
    "  <main>",
    '    <div class="mark">K</div>',
    "    <h1>Email confirmed</h1>",
    "    <p>Your KaamLink email is verified. Return to the app to finish setting your password.</p>",
    '    <a class="button" href="' + safeDeepLink + '">Open KaamLink</a>',
    '    <p class="hint">If the app did not open, copy this link:<br /><code>' + safeDeepLink + "</code></p>",
    "  </main>",
    "</body>",
    "</html>",
  ].join("\n");

  return new Response(html, {
    headers: {
      "content-type": "text/html; charset=utf-8",
      "cache-control": "no-store",
    },
  });
});
