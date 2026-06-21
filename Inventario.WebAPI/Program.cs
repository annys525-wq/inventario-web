using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.IdentityModel.Tokens;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Inventario.WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Puerto dinamico para Railway/Render ---
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// --- CORS: Permitir todo ---
builder.Services.AddCors(options =>
                         {
                                 options.AddPolicy("AllowAll", policy =>
                                                   {
                                                               policy.AllowAnyOrigin()
                                                                                 .AllowAnyMethod()
                                                                                 .AllowAnyHeader();
                                                   });
                         });

// --- Firebase Initialization ---
var credJson = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_JSON");
var projectId = builder.Configuration["Firebase:ProjectId"] ?? Environment.GetEnvironmentVariable("Firebase__ProjectId") ?? "inventario-1c03f";
FirestoreDb firestoreDb;

if (!string.IsNullOrEmpty(credJson))
{
        try
        {
                    var credDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(credJson);
                    if (credDict != null && credDict.TryGetValue("private_key", out var privateKeyObj))
                    {
                                    var privateKey = privateKeyObj?.ToString();
                                    if (privateKey != null && privateKey.Contains("\\n"))
                                    {
                                                        Console.WriteLine("Normalizing private_key containing literal \\n characters...");
                                                        credDict["private_key"] = privateKey.Replace("\\n", "\n");
                                                        credJson = System.Text.Json.JsonSerializer.Serialize(credDict);
                                    }
                    }
        }
        catch (Exception ex)
        {
                    Console.WriteLine($"Warning: Failed to parse/normalize FIREBASE_CREDENTIALS_JSON: {ex.Message}");
        }

        // Initialize FirebaseApp
        try
        {
                    FirebaseApp.Create(new AppOptions
                                       {
                                                       Credential = GoogleCredential.FromJson(credJson)
                                           });
                    Console.WriteLine("FirebaseApp initialized successfully.");
        }
        catch (Exception ex)
        {
                    Console.WriteLine($"Error initializing FirebaseApp: {ex.Message}");
        }

        // Initialize FirestoreDb with credential
        try
        {
                    var credential = GoogleCredential.FromJson(credJson);
                    firestoreDb = new FirestoreDbBuilder
                    {
                                    ProjectId = projectId,
                                    Credential = credential
                        }.Build();
                    Console.WriteLine("FirestoreDb initialized with credential successfully.");
        }
        catch (Exception ex)
        {
                    Console.WriteLine($"Error initializing FirestoreDb with credential: {ex.Message}. Falling back to default.");
                    firestoreDb = FirestoreDb.Create(projectId);
        }
}
else
{
        Console.WriteLine("FIREBASE_CREDENTIALS_JSON env var is not set. Using default credentials.");
        try
        {
                    FirebaseApp.Create();
        }
        catch (Exception ex)
        {
                    Console.WriteLine($"Warning: FirebaseApp.Create() failed: {ex.Message}");
        }
        firestoreDb = FirestoreDb.Create(projectId);
}

builder.Services.AddSingleton(firestoreDb);

// --- Servicios ---
builder.Services.AddHttpClient();
builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddScoped<AuthService>();

// --- JWT Authentication ---
builder.Services.AddAuthentication(options =>
                                   {
                                           options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                                           options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                                   })
    .AddJwtBearer(options =>
                  {
                          options.Authority = $"https://securetoken.google.com/{projectId}";
                          options.TokenValidationParameters = new TokenValidationParameters
                          {
                                      ValidateIssuer = true,
                                      ValidIssuer = $"https://securetoken.google.com/{projectId}",
                                      ValidateAudience = true,
                                      ValidAudience = projectId,
                                      ValidateLifetime = true
                              };
                  });

builder.Services.AddControllers()
        .AddJsonOptions(options =>
                        {
                                    options.JsonSerializerOptions.PropertyNamingPolicy = null;
                        });

var app = builder.Build();

if (Environment.GetEnvironmentVariable("SEED_ADMIN") == "true")
{
        using var scope = app.Services.CreateScope();
        var fDb = scope.ServiceProvider.GetRequiredService<FirestoreDb>();
        var auth = FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance;

        async Task SeedUserAsync(string email, string password, string username, string fullName, Inventario.WebAPI.Models.UserRole role)
        {
                    string uid;
                    try
                    {
                                    var argsUser = new FirebaseAdmin.Auth.UserRecordArgs
                                    {
                                                        Email = email,
                                                        Password = password,
                                                        DisplayName = fullName,
                                    };
                                    var userRecord = await auth.CreateUserAsync(argsUser);
                                    uid = userRecord.Uid;
                    }
                    catch (Exception)
                    {
                                    var userRecord = await auth.GetUserByEmailAsync(email);
                                    uid = userRecord.Uid;
                    }

                    var userDoc = fDb.Collection("users").Document(uid);
                    var newUser = new Inventario.WebAPI.Models.User
                    {
                                    Id = uid,
                                    Username = username,
                                    FullName = fullName,
                                    Email = email,
                                    Role = role,
                                    IsActive = true,
                                    CreatedAt = DateTime.UtcNow
                        };
                    await userDoc.SetAsync(newUser, SetOptions.MergeAll);
        }

        try
        {
                    await SeedUserAsync("admin@ejemplo.com", "adminpassword123", "admin", "Admin Default", Inventario.WebAPI.Models.UserRole.Administrador);
                    Console.WriteLine("======================================");
                    Console.WriteLine("ADMIINISTRADOR CREADO/CONFIGURADO EN FIRESTORE CON EXITO");
                    Console.WriteLine("======================================");

                    await SeedUserAsync("vendedor@ejemplo.com", "vendedorpassword123", "vendedor", "Juan Vendedor", Inventario.WebAPI.Models.UserRole.Vendedor);
                    Console.WriteLine("======================================");
                    Console.WriteLine("VENDEDOR CREADO/CONFIGURADO EN FIRESTORE CON EXITO");
                    Console.WriteLine("======================================");

                    await SeedUserAsync("bodega@ejemplo.com", "bodegapassword123", "bodega", "Marta Bodega", Inventario.WebAPI.Models.UserRole.Bodega);
                    Console.WriteLine("======================================");
                    Console.WriteLine("ENCARGADO DE BODEGA CREADO/CONFIGURADO EN FIRESTORE CON EXITO");
                    Console.WriteLine("======================================");

                    // Seed Products
                    try
                    {
                                    var productsColl = fDb.Collection("products");
                                    var seedProducts = new List<Inventario.WebAPI.Models.Product>
                                    {
                                                        new() { Id = "p1", EAN = "7702011028137", SKU = "PROD001", Name = "Computadora Portatil Core i7", Category = "Tecnologia", Cost = 3400000.00, Price = 4800000.00, WarehouseMain = 15, WarehouseSecondary = 3, MinimumStock = 5, UpdatedBy = "Seed" },
                                                        new() { Id = "p2", EAN = "7702011038137", SKU = "PROD002", Name = "Monitor UltraWide 29", Category = "Tecnologia", Cost = 720000.00, Price = 1080000.00, WarehouseMain = 4, WarehouseSecondary = 1, MinimumStock = 6, UpdatedBy = "Seed" },
                                                        new() { Id = "p3", EAN = "7702011048137", SKU = "PROD003", Name = "Silla Ergonomica Pro", Category = "Mobiliario", Cost = 480000.00, Price = 780000.00, WarehouseMain = 25, WarehouseSecondary = 10, MinimumStock = 8, UpdatedBy = "Seed" },
                                                        new() { Id = "p4", EAN = "7702011058137", SKU = "PROD004", Name = "Escritorio Elevable Electrico", Category = "Mobiliario", Cost = 1240000.00, Price = 1920000.00, WarehouseMain = 2, WarehouseSecondary = 0, MinimumStock = 3, UpdatedBy = "Seed" }
                                    };
                                    foreach (var prod in seedProducts)
                                    {
                                                        await productsColl.Document(prod.Id).SetAsync(prod, SetOptions.MergeAll);
                                    }
                                    Console.WriteLine("PRODUCTOS SEMILLA CONFIGURADOS CON PRECIOS EN COP EN FIRESTORE");
                    }
                    catch (Exception ex)
                    {
                                    Console.WriteLine("ERROR AL SEMBRAR PRODUCTOS: " + ex.Message);
                    }

                    // Seed Customers
                    try
                    {
                                    var customersColl = fDb.Collection("customers");
                                    var seedCustomers = new List<Inventario.WebAPI.Models.Customer>
                                    {
                                                        new() { Id = "c1", FullName = "ACME Corp Colombia", TaxId = "900.123.456-1", Email = "compras@acme.com.co", Phone = "+57 300 123 4567", PipelineStage = "Cerrado", CreditLimit = 20000000.00, OutstandingBalance = 5000000.00, UpdatedBy = "Seed" },
                                                        new() { Id = "c2", FullName = "Distribuciones Globales S.A.S", TaxId = "830.987.654-2", Email = "proveedores@global.com", Phone = "+57 315 987 6543", PipelineStage = "Propuesta", CreditLimit = 48000000.00, OutstandingBalance = 0.00, UpdatedBy = "Seed" },
                                                        new() { Id = "c3", FullName = "Industrias Metalmecanicas Luna", TaxId = "901.444.888-0", Email = "contacto@metalluna.com", Phone = "+57 320 444 8888", PipelineStage = "Prospecto", CreditLimit = 8000000.00, OutstandingBalance = 1800000.00, UpdatedBy = "Seed" }
                                    };
                                    foreach (var cust in seedCustomers)
                                    {
                                                        await customersColl.Document(cust.Id).SetAsync(cust, SetOptions.MergeAll);
                                    }
                                    Console.WriteLine("CLIENTES SEMILLA CONFIGURADOS CON LIMITES EN COP EN FIRESTORE");
                    }
                    catch (Exception ex)
                    {
                                    Console.WriteLine("ERROR AL SEMBRAR CLIENTES: " + ex.Message);
                    }

                    // Seed Suppliers
                    try
                    {
                                    var suppliersColl = fDb.Collection("suppliers");
                                    var suppliersSnapshot = await suppliersColl.Limit(1).GetSnapshotAsync();
                                    if (suppliersSnapshot.Count == 0)
                                    {
                                                        var seedSuppliers = new List<Inventario.WebAPI.Models.Supplier>
                                                        {
                                                                                new() { Id = "prov1", FullName = "Tecnologia y Suministros S.A.", TaxId = "890.222.111-4", Email = "contacto@tecno-suministros.com", Phone = "+57 311 222 3333", Address = "Av. 45 #88-12, Bogota", ContactPerson = "Carlos Perez", UpdatedBy = "Seed" },
                                                                                new() { Id = "prov2", FullName = "Distribuidora Mobiliaria Mayorista", TaxId = "900.555.777-2", Email = "ventas@distrimobiliaria.co", Phone = "+57 314 999 8888", Address = "Calle 15 #30-45, Medellin", ContactPerson = "Ana Maria Gomez", UpdatedBy = "Seed" }
                                                        };
                                                        foreach (var supp in seedSuppliers)
                                                        {
                                                                                await suppliersColl.Document(supp.Id).SetAsync(supp);
                                                        }
                                                        Console.WriteLine("PROVEEDORES SEMILLA CREADOS EN FIRESTORE");
                                    }
                    }
                    catch (Exception ex)
                    {
                                    Console.WriteLine("ERROR AL SEMBRAR PROVEEDORES: " + ex.Message);
                    }
        }
        catch (Exception e)
        {
                    Console.WriteLine("ERROR AL CREAR ADMIN: " + e.Message);
        }
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
