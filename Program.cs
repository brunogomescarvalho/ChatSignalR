using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ChatService>();
builder.Services.AddSignalR();

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

class ChatHub(ChatService chatService) : Hub
{
    private readonly ChatService _chatService = chatService;

    public async Task IncluirUsuario(string nome)
    {
        try
        {
            _chatService.IncluirUsuario(nome);

            // Notifica todos os outros que um novo usuário entrou
            await Clients.AllExcept(Context.ConnectionId).SendAsync("UsuarioIncluido", nome);

            // Envia lista atualizada apenas para o Caller
            await Clients.Caller.SendAsync("UsuariosAtuais", _chatService.ObterUsuarios());
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ErroAoIncluirUsuario", ex.Message);
        }

    }


    public async Task EnviarMensagem(string nome, string mensagem)
    {
        var data = DateTime.Now.ToLongTimeString();
        await Clients.All.SendAsync("MensagemRecebida", nome, mensagem, data);
    }

    public async Task RemoverUsuario(string nome)
    {
        _chatService.RemoverUsuario(nome);
        await Clients.AllExcept(Context.ConnectionId).SendAsync("UsuarioRemovido", nome);
    }

    public async Task<List<string>> ObterUsuarios()
    {
        var usuarios = _chatService.ObterUsuarios();
        return await Task.FromResult(usuarios);
    }
}

class ChatService
{
    private readonly List<string> Usuarios = [];

    public void IncluirUsuario(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome inválido.");

        if (!Usuarios.Contains(nome, StringComparer.OrdinalIgnoreCase))
            Usuarios.Add(nome);

        else
            throw new InvalidOperationException("Usuário já está na sala.");

    }

    public List<string> ObterUsuarios()
    {
        return Usuarios;
    }

    public void RemoverUsuario(string nome)
    {
        Usuarios.Remove(nome);
    }
}