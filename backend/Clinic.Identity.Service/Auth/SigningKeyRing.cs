using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Clinic.Identity.Service.Auth;

public sealed class SigningKeyRing
{
    private readonly object _sync = new();
    private readonly string _directory;
    private readonly List<RSA> _keys = [];
    private RSA _active = null!;
    private string _activeKid = "";

    public SigningKeyRing(IConfiguration configuration, IHostEnvironment environment)
    {
        _directory = configuration["Jwt:SigningKeyDirectory"]
            ?? Path.Combine(environment.ContentRootPath, "var", "identity-keys");
        Directory.CreateDirectory(_directory);
        LoadOrCreate();
    }

    public string ActiveKid { get { lock (_sync) return _activeKid; } }

    public SigningCredentials Credentials
    {
        get
        {
            lock (_sync)
                return new SigningCredentials(new RsaSecurityKey(_active) { KeyId = _activeKid }, SecurityAlgorithms.RsaSha256);
        }
    }

    public IEnumerable<SecurityKey> ValidationKeys
    {
        get { lock (_sync) return _keys.Select(key => (SecurityKey)new RsaSecurityKey(key) { KeyId = GetKid(key) }).ToArray(); }
    }

    public IReadOnlyList<Models.JwkResponse> PublicKeys()
    {
        lock (_sync)
        {
            return _keys.Select(key =>
            {
                var p = key.ExportParameters(false);
                return new Models.JwkResponse(GetKid(key), "RSA", "sig", "RS256", Base64UrlEncoder.Encode(p.Modulus!), Base64UrlEncoder.Encode(p.Exponent!));
            }).ToArray();
        }
    }

    public string Rotate()
    {
        lock (_sync)
        {
            var next = RSA.Create(3072);
            var path = Path.Combine(_directory, $"{GetKid(next)}.pem");
            File.WriteAllText(path, next.ExportPkcs8PrivateKeyPem());
            _keys.Add(next);
            _active = next;
            _activeKid = GetKid(next);
            return _activeKid;
        }
    }

    private void LoadOrCreate()
    {
        foreach (var path in Directory.EnumerateFiles(_directory, "*.pem"))
        {
            var key = RSA.Create();
            key.ImportFromPem(File.ReadAllText(path));
            _keys.Add(key);
        }
        if (_keys.Count == 0)
        {
            var key = RSA.Create(3072);
            File.WriteAllText(Path.Combine(_directory, $"{GetKid(key)}.pem"), key.ExportPkcs8PrivateKeyPem());
            _keys.Add(key);
        }
        _active = _keys.OrderByDescending(k => File.GetLastWriteTimeUtc(Path.Combine(_directory, $"{GetKid(k)}.pem"))).First();
        _activeKid = GetKid(_active);
    }

    private static string GetKid(RSA key)
    {
        var p = key.ExportParameters(false);
        var hash = SHA256.HashData(p.Modulus!);
        return Base64UrlEncoder.Encode(hash[..12]);
    }
}
