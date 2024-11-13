namespace FLLSlides.Pages;

/// <summary>
/// Represents the main page of the application.
/// </summary>
public partial class Index : IBrowserViewportObserver, IAsyncDisposable
{
    /// <summary>
    /// Gets or sets the dialog service.
    /// </summary>
    [Inject] private IDialogService DialogService { get; set; }

    /// <summary>
    /// Gets or sets the browser viewport service.
    /// </summary>
    [Inject] private IBrowserViewportService BrowserViewportService { get; set; }

    /// <summary>
    /// Gets or sets the HTTP client factory.
    /// </summary>
    [Inject] private IHttpClientFactory ClientFactory { get; set; }

    /// <summary>
    /// Gets or sets the profile.
    /// </summary>
    private RequestModel Profile { get; set; }

    /// <summary>
    /// Gets or sets the teams.
    /// </summary>
    private string Teams { get; set; }

    /// <summary>
    /// Gets the list of errors.
    /// </summary>
    private List<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the schedule is being generated.
    /// </summary>
    private bool Generating { get; set; } = false;

    /// <summary>
    /// Gets or sets the response model.
    /// </summary>
    private ResponseModel Response;

    /// <summary>
    /// Called after the component has rendered.
    /// </summary>
    /// <param name="firstRender">Indicates whether this is the first render.</param>
    protected override async void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            await DoProfileSelected(Profiles[0]);
            StateHasChanged();
        }
    }

    /// <summary>
    /// Handles the profile selection.
    /// </summary>
    /// <param name="value">The selected profile.</param>
    private async Task DoProfileSelected(RequestModel value)
    {
        Generating = true;
        await Task.Run(async () =>
        {
            Profile = value;
            Teams = string.Join(Environment.NewLine, Profile.Teams.Select(t => $"{t.Number}, {t.Name}"));
            await ServerReload();
            Generating = false;
        });
    }

    /// <summary>
    /// Updates the schedule based on the modifications in the UI.
    /// </summary>
    private async Task DoGenerateSlides()
    {
        Generating = true;
        await Task.Run(async () =>
        {
            var profile = new RequestModel
            {
                Teams = Teams.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(t => t.Split(",;\t ".ToCharArray(), 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Select(pair => new Team { Number = Convert.ToInt32(pair[0]), Name = pair[1] })
                    .ToArray()
            };
            profile.Name = $"Customized: {profile.Teams.Length} Teams";

            var existing = Profiles.FirstOrDefault(p => p.Name == profile.Name);
            if (existing != null)
            {
                Profiles.Remove(existing);
            }
            Profiles.Insert(0, profile);
            Profile = profile;

            await ServerReload();
            Generating = false;
        });
    }

    /// <summary>
    /// Reloads the server data.
    /// </summary>
    private async Task ServerReload()
    {
        if (!ConfigIsValid())
        {
            return;
        }

        var httpClient = ClientFactory.CreateClient("API");
        using var response = await httpClient.PostAsJsonAsync("api/GenerateDeck", Profile);
        if (response.IsSuccessStatusCode)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync();
            await BlazorDownloadFileService.DownloadFile("Slides.pptx", bytes, "application/powerpoint");
        }
    }

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>True if the configuration is valid; otherwise, false.</returns>
    private bool ConfigIsValid()
    {
        Errors.Clear();
        if (Profile == null) Errors.Add("Invalid configuration");
        return Errors.Count == 0;
    }

    [Inject] IBlazorDownloadFileService BlazorDownloadFileService { get; set; }

    private async Task<IEnumerable<RequestModel>> IdentifyProfiles(string value, CancellationToken token)
    {
        // if text is null or empty, show complete list
        if (string.IsNullOrEmpty(value)) return Profiles;
        return await Task.FromResult(Profiles
            .Where(x => x.Name.Contains(value, StringComparison.InvariantCultureIgnoreCase))
            .AsEnumerable());
    }

    /// <summary>
    /// Executes after the component is rendered.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await BrowserViewportService.SubscribeAsync(this, fireImmediately: true);
            OpenWelcomeDialog();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    /// <summary>
    /// Opens the welcome dialog.
    /// </summary>
    private void OpenWelcomeDialog() => DialogService.Show<WelcomeDialog>("Welcome", new DialogOptions
    {
        MaxWidth = MaxWidth.Large,
        CloseButton = true,
        BackdropClick = false,
        NoHeader = false,
        Position = DialogPosition.Center,
        CloseOnEscapeKey = true
    });

    /// <summary>
    /// Gets the ID of the browser viewport observer.
    /// </summary>
    Guid IBrowserViewportObserver.Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the resize options of the browser viewport observer.
    /// </summary>
    ResizeOptions IBrowserViewportObserver.ResizeOptions { get; } = new()
    {
        ReportRate = 1000,
        NotifyOnBreakpointOnly = false
    };

    /// <summary>
    /// Notifies the browser viewport change.
    /// </summary>
    Task IBrowserViewportObserver.NotifyBrowserViewportChangeAsync(BrowserViewportEventArgs browserViewportEventArgs)
    {
        //_width = browserViewportEventArgs.BrowserWindowSize.Width;
        //_height = browserViewportEventArgs.BrowserWindowSize.Height;
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Disposes the component.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await BrowserViewportService.UnsubscribeAsync(this);
        GC.SuppressFinalize(this);
    }

    private static readonly List<RequestModel> Profiles = new[]
    {
        (teamcount: 6, tablecount: 2),
        (teamcount: 12, tablecount: 2),
        (teamcount: 18, tablecount: 4),
        (teamcount: 24, tablecount: 4),
        (teamcount: 36, tablecount: 6),
        (teamcount: 48, tablecount: 6),
        (teamcount: 60, tablecount: 10)
    }
    .Select(e => BuildProfile(e.teamcount, e.tablecount))
    .ToList();

    private static RequestModel BuildProfile(int teamcount, int tablecount)
    {
        var allteams = Enumerable.Range(1001, teamcount)
            .Select(i => new Team { Number = i, Name = $"team {i:0000}" })
            .ToArray();
        return new RequestModel
        {
            Name = $"{teamcount} Teams",
            Teams = allteams
        };
    }
}