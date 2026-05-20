using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MarketingFloy.ApiService.Data;
using MarketingFloy.ApiService.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// SQLite – la DB se guarda junto al ejecutable
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "marketingfloy.db");
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

// JWT
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "MarketingFloySecretKey2025XYZ!@#$%^&*()_+ABCDEF1234567890";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// CORS – permite cualquier origen, incluyendo file:// y GitHub Pages
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ── Auto-crear tablas y seed del usuario admin ──────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    var adminUser = db.AdminUsers.FirstOrDefault(u => u.Username == "admin");
    if (adminUser is null)
    {
        db.AdminUsers.Add(new AdminUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Floy2025!"),
            FechaCreacion = DateTime.UtcNow
        });
        db.SaveChanges();
    }
    else if (!BCrypt.Net.BCrypt.Verify("Floy2025!", adminUser.PasswordHash))
    {
        // Hash inválido (DB creada antes de BCrypt) — lo corregimos
        adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Floy2025!");
        db.SaveChanges();
    }

    // Asegurar tabla ContentItems aunque DB ya existiera
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ContentItems (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Key TEXT NOT NULL,
            Value TEXT NOT NULL DEFAULT '',
            Type TEXT NOT NULL DEFAULT 'text',
            UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        )");

    if (!db.ContentItems.Any())
    {
        db.ContentItems.AddRange(new[]
        {
            // Hero
            new ContentItem { Key="hero.badge",       Value="Portafolio Profesional",                                                      Type="text" },
            new ContentItem { Key="hero.name",        Value="Floymer<br/>Bencomo",                                                         Type="html" },
            new ContentItem { Key="hero.subtitle",    Value="Coordinadora Administrativa &amp; RRHH · Marketing Digital · Gestión Operativa · Creación de Contenido", Type="text" },
            new ContentItem { Key="hero.description", Value="Profesional multidisciplinaria con experiencia en recursos humanos, administración, operaciones y marketing digital. Especializada en gestión de equipos, creación de contenido, estrategias comerciales y optimización de procesos en entornos gastronómicos y comerciales.", Type="text" },
            // Sobre mí – stats
            new ContentItem { Key="about.stat1.number", Value="5+",                    Type="text" },
            new ContentItem { Key="about.stat1.label",  Value="Años de experiencia",   Type="text" },
            new ContentItem { Key="about.stat2.number", Value="4",                     Type="text" },
            new ContentItem { Key="about.stat2.label",  Value="Marcas gestionadas",    Type="text" },
            new ContentItem { Key="about.stat3.number", Value="3",                     Type="text" },
            new ContentItem { Key="about.stat3.label",  Value="Áreas de expertise",    Type="text" },
            new ContentItem { Key="about.stat4.number", Value="100%",                  Type="text" },
            new ContentItem { Key="about.stat4.label",  Value="Compromiso",            Type="text" },
            // Sobre mí – párrafos
            new ContentItem { Key="about.heading", Value="Me apasiona construir, organizar y mejorar todo lo que toco.", Type="text" },
            new ContentItem { Key="about.p1", Value="Mi experiencia combina pensamiento estratégico, creatividad y gestión operativa, permitiéndome desenvolverme con soltura en áreas administrativas, talento humano y marketing digital.", Type="text" },
            new ContentItem { Key="about.p2", Value="No soy \"solo marketing\". Soy una mezcla rara y valiosa de habilidades que pocas profesionales combinan: marketing + RRHH + administración + operaciones + contenido + gastronomía + creatividad.", Type="text" },
            new ContentItem { Key="about.p3", Value="Cada empresa donde he estado ha encontrado en mí a alguien que resuelve, ejecuta y lidera con resultados medibles.", Type="text" },
            // Contacto
            new ContentItem { Key="contact.phone",     Value="(849) 279-9533",     Type="text" },
            new ContentItem { Key="contact.phone.href",Value="+18492799533",        Type="text" },
            new ContentItem { Key="contact.email",     Value="florymer02@gmail.com", Type="text" },
            new ContentItem { Key="contact.instagram", Value="@floymer.bencomo",    Type="text" },
            new ContentItem { Key="contact.linkedin",  Value="Floymer Bencomo",     Type="text" },
        });
        db.SaveChanges();
    }
}

// ── Middleware ───────────────────────────────────────────────────────────────
app.UseExceptionHandler();
app.UseCors();

// Servir imágenes subidas desde /uploads
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();

// ═══════════════════════════════════════════════════════════════════════════
// ENDPOINTS
// ═══════════════════════════════════════════════════════════════════════════

app.MapGet("/", () => "MarketingFloy API v1.0 ✔");

// ── AUTH ─────────────────────────────────────────────────────────────────────

app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db, IConfiguration config) =>
{
    var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == req.Username);
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var key = config["Jwt:Key"] ?? "MarketingFloySecretKey2025XYZ!@#$%^&*()_+ABCDEF1234567890";
    var tokenHandler = new JwtSecurityTokenHandler();
    var descriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity([new Claim(ClaimTypes.Name, user.Username)]),
        Expires = DateTime.UtcNow.AddHours(8),
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.WriteToken(tokenHandler.CreateToken(descriptor));
    return Results.Ok(new { token, username = user.Username });
});

// ── CONTACT ──────────────────────────────────────────────────────────────────

app.MapPost("/api/contact", async (ContactRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Nombre) ||
        string.IsNullOrWhiteSpace(req.Email) ||
        string.IsNullOrWhiteSpace(req.Mensaje))
        return Results.BadRequest(new { error = "Todos los campos son requeridos." });

    db.ContactMessages.Add(new ContactMessage
    {
        Nombre = req.Nombre.Trim(),
        Email = req.Email.Trim(),
        Mensaje = req.Mensaje.Trim(),
        FechaCreacion = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.Ok(new { success = true, message = "¡Mensaje recibido! Te contactaré pronto." });
});

app.MapGet("/api/contact", async (AppDbContext db) =>
    Results.Ok(await db.ContactMessages
        .OrderByDescending(m => m.FechaCreacion)
        .ToListAsync()))
    .RequireAuthorization();

app.MapPut("/api/contact/{id:int}/read", async (int id, AppDbContext db) =>
{
    var msg = await db.ContactMessages.FindAsync(id);
    if (msg is null) return Results.NotFound();
    msg.Leido = true;
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.MapDelete("/api/contact/{id:int}", async (int id, AppDbContext db) =>
{
    var msg = await db.ContactMessages.FindAsync(id);
    if (msg is null) return Results.NotFound();
    db.ContactMessages.Remove(msg);
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

// ── GALLERY (público) ─────────────────────────────────────────────────────────

app.MapGet("/api/gallery", async (AppDbContext db) =>
    Results.Ok(await db.GalleryImages
        .Where(g => g.Activo)
        .OrderBy(g => g.Orden)
        .ToListAsync()));

// ── GALLERY (admin) ───────────────────────────────────────────────────────────

app.MapGet("/api/gallery/all", async (AppDbContext db) =>
    Results.Ok(await db.GalleryImages
        .OrderBy(g => g.Orden)
        .ToListAsync()))
    .RequireAuthorization();

app.MapPost("/api/gallery/upload", async (HttpContext ctx, AppDbContext db, IWebHostEnvironment env) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("imagen");

    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No se proporcionó imagen." });

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext))
        return Results.BadRequest(new { error = "Formato no soportado. Usa JPG, PNG, GIF o WebP." });

    var dir = Path.Combine(env.ContentRootPath, "uploads");
    Directory.CreateDirectory(dir);
    var fileName = $"{Guid.NewGuid()}{ext}";
    var filePath = Path.Combine(dir, fileName);

    await using (var stream = File.Create(filePath))
        await file.CopyToAsync(stream);

    var titulo = form["titulo"].ToString();
    var descripcion = form["descripcion"].ToString();
    _ = int.TryParse(form["orden"], out int orden);

    var img = new GalleryImage
    {
        Titulo = string.IsNullOrEmpty(titulo) ? fileName : titulo,
        Descripcion = string.IsNullOrEmpty(descripcion) ? null : descripcion,
        UrlImagen = $"/uploads/{fileName}",
        Orden = orden,
        Activo = true,
        FechaSubida = DateTime.UtcNow
    };
    db.GalleryImages.Add(img);
    await db.SaveChangesAsync();
    return Results.Ok(img);
}).RequireAuthorization();

app.MapPut("/api/gallery/{id:int}", async (int id, GalleryUpdateRequest req, AppDbContext db) =>
{
    var img = await db.GalleryImages.FindAsync(id);
    if (img is null) return Results.NotFound();
    if (req.Titulo is not null) img.Titulo = req.Titulo;
    img.Descripcion = req.Descripcion;
    img.Orden = req.Orden;
    img.Activo = req.Activo;
    await db.SaveChangesAsync();
    return Results.Ok(img);
}).RequireAuthorization();

app.MapDelete("/api/gallery/{id:int}", async (int id, AppDbContext db, IWebHostEnvironment env) =>
{
    var img = await db.GalleryImages.FindAsync(id);
    if (img is null) return Results.NotFound();

    var filePath = Path.Combine(env.ContentRootPath,
        img.UrlImagen.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    if (File.Exists(filePath)) File.Delete(filePath);

    db.GalleryImages.Remove(img);
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

// ── VISITAS ───────────────────────────────────────────────────────────────────

app.MapPost("/api/visits", async (VisitRequest req, HttpContext ctx, AppDbContext db) =>
{
    db.VisitLogs.Add(new VisitLog
    {
        IpAddress = ctx.Connection.RemoteIpAddress?.ToString(),
        UserAgent = ctx.Request.Headers.UserAgent.ToString(),
        Pagina = req.Pagina,
        FechaVisita = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapGet("/api/visits/stats", async (AppDbContext db) =>
{
    var total = await db.VisitLogs.CountAsync();
    var today = DateTime.UtcNow.Date;
    var hoy = await db.VisitLogs
        .CountAsync(v => v.FechaVisita >= today && v.FechaVisita < today.AddDays(1));
    var unread = await db.ContactMessages.CountAsync(m => !m.Leido);
    var gallery = await db.GalleryImages.CountAsync();
    return Results.Ok(new { total, hoy, unread, gallery });
}).RequireAuthorization();

// ── CONTENT (edición inline) ──────────────────────────────────────────────────

// Público: devuelve todo el contenido
app.MapGet("/api/content", async (AppDbContext db) =>
    Results.Ok(await db.ContentItems.ToListAsync()));

// Admin: actualiza un campo por key (crea si no existe)
app.MapPatch("/api/content/{key}", async (string key, ContentUpdateRequest req, AppDbContext db) =>
{
    var item = await db.ContentItems.FirstOrDefaultAsync(c => c.Key == key);
    if (item is null)
    {
        item = new ContentItem { Key = key, Value = req.Value, Type = req.Type ?? "text", UpdatedAt = DateTime.UtcNow };
        db.ContentItems.Add(item);
    }
    else
    {
        item.Value = req.Value;
        if (req.Type is not null) item.Type = req.Type;
        item.UpdatedAt = DateTime.UtcNow;
    }
    await db.SaveChangesAsync();
    return Results.Ok(item);
}).RequireAuthorization();

// Admin: sube imagen asociada a una key y la guarda en ContentItems
app.MapPost("/api/images/{key}", async (string key, HttpContext ctx, AppDbContext db, IWebHostEnvironment env) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("image");
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No se proporcionó imagen." });

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext))
        return Results.BadRequest(new { error = "Formato no soportado." });

    var dir = Path.Combine(env.ContentRootPath, "uploads");
    Directory.CreateDirectory(dir);
    var safeKey = key.Replace(".", "-").Replace("/", "-");
    var fileName = $"{safeKey}-{Guid.NewGuid()}{ext}";
    var filePath = Path.Combine(dir, fileName);

    await using (var stream = File.Create(filePath))
        await file.CopyToAsync(stream);

    var url = $"/uploads/{fileName}";

    var item = await db.ContentItems.FirstOrDefaultAsync(c => c.Key == key);
    if (item is null)
    {
        item = new ContentItem { Key = key, Value = url, Type = "image", UpdatedAt = DateTime.UtcNow };
        db.ContentItems.Add(item);
    }
    else
    {
        item.Value = url;
        item.Type = "image";
        item.UpdatedAt = DateTime.UtcNow;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new { url, key });
}).RequireAuthorization();

// Admin: cambiar contraseña
app.MapPost("/api/auth/change-password", async (ChangePasswordRequest req, HttpContext ctx, AppDbContext db) =>
{
    var username = ctx.User.Identity?.Name;
    var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);
    if (user is null) return Results.Unauthorized();
    if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
        return Results.BadRequest(new { error = "Contraseña actual incorrecta." });
    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
    await db.SaveChangesAsync();
    return Results.Ok(new { success = true });
}).RequireAuthorization();

app.MapDefaultEndpoints();
app.Run();

// ── Request records ───────────────────────────────────────────────────────────
record LoginRequest(string Username, string Password);
record ContactRequest(string Nombre, string Email, string Mensaje);
record VisitRequest(string? Pagina);
record GalleryUpdateRequest(string? Titulo, string? Descripcion, int Orden, bool Activo);
record ContentUpdateRequest(string Value, string? Type);
record ChangePasswordRequest(string CurrentPassword, string NewPassword);
