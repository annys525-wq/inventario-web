using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Inventario.WebAPI.Models;

namespace Inventario.WebAPI.Services
{
    public class AuthService
    {
        private readonly FirestoreService _db;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public AuthService(FirestoreService db, IConfiguration configuration, HttpClient httpClient)
        {
            _db = db;
            _httpClient = httpClient;
            // The API key should be provided in appsettings.json or via environment variables
            _apiKey = configuration["Firebase:ApiKey"] ?? Environment.GetEnvironmentVariable("FIREBASE_API_KEY") ?? "";
        }

        public async Task<(bool success, string? token, User? user)> LoginAsync(string username, string password)
        {
            // 1. Buscar el email del usuario usando su username en Firestore
            User? user = await _db.GetUserByUsernameAsync(username);

            if (user == null || !user.IsActive)
            {
                return (false, null, null);
            }

            string? token = null;

            // 2. Usar Firebase Auth REST API para verificar email/password y obtener ID Token
            if (string.IsNullOrEmpty(_apiKey))
            {
                // Fallback para desarrollo local si no se ha configurado la API Key de Firebase
                if ((username == "admin" && (password == "adminpassword123" || password == "admin123")) ||
                    (username == "vendedor" && (password == "vendedorpassword123" || password == "vendedor123")) ||
                    (username == "bodega" && (password == "bodegapassword123" || password == "bodega123")))
                {
                    token = "local-dev-token-bypass";
                }
                else
                {
                    Console.WriteLine($"WARNING: Firebase API Key is missing. Denied local login fallback for username: {username}");
                }
            }
            else
            {
                token = await VerifyPasswordWithFirebaseAsync(user.Email, password);
            }
            
            if (string.IsNullOrEmpty(token))
            {
                return (false, null, null);
            }

            return (true, token, user);
        }

        private async Task<string?> VerifyPasswordWithFirebaseAsync(string email, string password)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Console.WriteLine("ERROR: Firebase API Key is missing. Cannot authenticate.");
                return null;
            }

            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}";
            
            var payload = new
            {
                email = email,
                password = password,
                returnSecureToken = true
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(responseString);
                    if (doc.RootElement.TryGetProperty("idToken", out var idTokenElement))
                    {
                        return idTokenElement.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to Firebase Auth: {ex.Message}");
            }

            return null;
        }
    }
}
