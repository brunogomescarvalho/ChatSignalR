using Microsoft.AspNetCore.SignalR;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSignalR();
        builder.Services.AddSingleton<UsuariosService>();

        builder.Services.AddCors(opt => opt.AddPolicy("CorsPolicy", policy =>
        {
            policy.WithOrigins("http://127.0.0.1:5500")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }));

        var app = builder.Build();

        app.UseCors("CorsPolicy");

        app.MapHub<ChatHub>("/chat");

        app.Run();
    }
}

class ChatHub(UsuariosService usuariosService) : Hub
{
    private readonly UsuariosService _usuariosService = usuariosService;

    public async Task CriarChat(string nomeUsuario)
    {
        string salaId = Guid.NewGuid().ToString();
        var cToken = Context.ConnectionAborted;

        UsuarioConectado usuario = new()
        {
            ConnectionId = Context.ConnectionId,
            Nome = nomeUsuario,
            SalaId = salaId
        };

        var erros = usuario.Validar();
        if (erros.Count != 0)
        {
            await Clients.Caller.SendAsync("ErroAoIncluirUsuario", string.Join("\n", erros), cToken);
            return;
        }

        _usuariosService.Adicionar(usuario);

        await Groups.AddToGroupAsync(Context.ConnectionId, salaId, cToken);
        await Clients.Caller.SendAsync("ReceberIdSala", salaId, cToken);
        await Clients.Caller.SendAsync("UsuariosAtualizados", _usuariosService.ObterUsuariosPorSala(salaId), cToken);
    }

    public async Task EntrarChat(string nomeUsuario, string salaId)
    {
        var cToken = Context.ConnectionAborted;

        UsuarioConectado usuario = new()
        {
            ConnectionId = Context.ConnectionId,
            Nome = nomeUsuario,
            SalaId = salaId
        };

        var erros = usuario.Validar();
        if (erros.Count != 0)
        {
            await EnviarErro(string.Join("\n", erros), cToken);
            return;
        }

        if (_usuariosService.UsuarioExiste(nomeUsuario, salaId))
        {
            await EnviarErro("Usuário já está no chat", cToken);
            return;
        }

        if (!_usuariosService.SalaExiste(salaId))
        {
            await EnviarErro("Sala não encontrada", cToken);
            return;
        }

        _usuariosService.Adicionar(usuario);

        await Groups.AddToGroupAsync(Context.ConnectionId, salaId, cToken);
        await Clients.GroupExcept(salaId, Context.ConnectionId).SendAsync("UsuarioIncluido", nomeUsuario, cToken);
        await Clients.Caller.SendAsync("ReceberIdSala", salaId, cToken);
        await Clients.Group(salaId).SendAsync("UsuariosAtualizados", _usuariosService.ObterUsuariosPorSala(salaId), cToken);
    }

    public async Task EnviarMensagem(string nome, string mensagem, string sala)
    {
        var data = DateTime.Now.ToLongTimeString();
        var cToken = Context.ConnectionAborted;

        await Clients.Group(sala).SendAsync("MensagemRecebida", nome, mensagem, data, cToken);
    }

    public async Task RemoverUsuario(string nome, string salaId)
    {
        var cToken = Context.ConnectionAborted;

        _usuariosService.Remover(nome, salaId);
        await Clients.GroupExcept(salaId, Context.ConnectionId).SendAsync("UsuarioRemovido", nome, cToken);

        if (_usuariosService.ObterUsuariosPorSala(salaId).Count == 0)
            await Clients.Caller.SendAsync("SalaEncerrada", "A sala foi encerrada", cToken);

    }

    private async Task EnviarErro(string erro, CancellationToken cToken)
    {
        await Clients.Caller.SendAsync("ErroAoIncluirUsuario", erro, cToken);
    }
}

internal class UsuarioConectado
{
    public string ConnectionId = string.Empty;
    public string Nome = string.Empty;
    public string SalaId = string.Empty;

    public List<string> Validar()
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(Nome))
            erros.Add("O nome do usuário não pode ser vazio.");

        if (string.IsNullOrWhiteSpace(SalaId))
            erros.Add("O ID da sala não pode ser vazio.");

        return erros;
    }
}

class UsuariosService
{
    private readonly List<UsuarioConectado> _usuarios = [];

    public void Adicionar(UsuarioConectado usuario) => _usuarios.Add(usuario);

    public void Remover(string nome, string salaId)
    {
        var usuario = _usuarios.FirstOrDefault(u => u.Nome == nome && u.SalaId == salaId);
        if (usuario != null)
            _usuarios.Remove(usuario);
    }

    public List<string> ObterUsuariosPorSala(string salaId)
    {
        return _usuarios
            .Where(u => u.SalaId == salaId)
            .Select(u => u.Nome)
            .ToList();
    }

    public bool SalaExiste(string salaId) =>
        _usuarios.Any(u => u.SalaId == salaId);

    public bool UsuarioExiste(string nome, string salaId) =>
        _usuarios.Any(u => u.SalaId == salaId && u.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase));
}
