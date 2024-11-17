namespace FLLSlides.Pages;

/// <summary>
/// Represents the main page of the application.
/// </summary>
public partial class Index
{
    /// <summary>
    /// Gets or sets the dialog service.
    /// </summary>
    [Inject] private IDialogService DialogService { get; set; }

    /// <summary>
    /// Gets or sets the HTTP client factory.
    /// </summary>
    [Inject] private IHttpClientFactory ClientFactory { get; set; }

    [Inject] IBlazorDownloadFileService BlazorDownloadFileService { get; set; }

    /// <summary>
    /// Gets or sets the profile.
    /// </summary>
    private TemplateDetails Profile { get; set; } = new TemplateDetails();

    /// <summary>
    /// Gets or sets the teams.
    /// </summary>
    private string Teams { get; set; } = string.Join(Environment.NewLine, Enumerable.Range(1001, 24).Select(i => $"{i:0000}, team {i:0000}"));

    /// <summary>
    /// Gets or sets a value indicating whether the schedule is being generated.
    /// </summary>
    private bool Generating { get; set; } = false;

    /// <summary>
    /// Handles the profile selection.
    /// </summary>
    /// <param name="value">The selected profile.</param>
    private void DoProfileSelected(TemplateDetails value)
    {
        Profile = value;
    }

    private readonly Dictionary<string, MudAutocomplete<Team>> autocompletes = [];

    private readonly Dictionary<string, string> subs = [];
    private void DoFieldValueSelected(string field, Team team)
    {
        if (team.Number != 0)
        {
            subs[field] = $"{team.Name} ({team.Number})";
        }
    }

    private void DoOtherFieldValueSelected(string field, string value)
    {
        subs[field] = value.Trim();
    }

    private async Task<IEnumerable<Team>> IdentifyTeams(string value, CancellationToken token)
    {
        var teams = Teams.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Split(",;\t ".ToCharArray(), 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(pair => new Team { Number = Convert.ToInt32(pair[0]), Name = pair[1] })
            .ToList();
        teams.Insert(0, new Team { Name = "Other (enter value to the right) --->", Number = 0 });
        return await Task.FromResult(teams
            .Where(t => value == null || t.Name.Contains(value, StringComparison.InvariantCultureIgnoreCase))
            .AsEnumerable());
    }

    /// <summary>
    /// Generates the resulting PPTX.
    /// </summary>
    private async Task DoGenerateSlides()
    {
        Generating = true;
        await Task.Run(async () =>
        {
            var httpClient = ClientFactory.CreateClient("API");
            using var response = await httpClient.PostAsJsonAsync("api/GenerateDeck", new RequestModel
            {
                Name = Profile.Name,
                TemplateDetails = Profile,
                Substitutions = subs
            });

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                await BlazorDownloadFileService.DownloadFile("Slides.pptx", bytes, "application/vnd.openxmlformats-officedocument.presentationml.presentation");
            }
            Generating = false;
        });
    }

    private TemplateResponse templates;
    private async Task<IEnumerable<TemplateDetails>> IdentifyProfiles(string value, CancellationToken token)
    {
        if (templates == null)
        {
            var httpClient = ClientFactory.CreateClient("API");
            using var response = await httpClient.PostAsJsonAsync("api/GetTemplateDetails", new TemplateRequest { Name = "Web UI" }, token);
            if (response.IsSuccessStatusCode)
            {
                templates = await response.Content.ReadFromJsonAsync<TemplateResponse>(token);
                if (templates.Templates.Length != 0 && Profile.Name == null)
                {
                    Profile = templates.Templates.First();
                    StateHasChanged();
                }
            }
        }
        return await Task.FromResult(templates.Templates
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
            var _ = IdentifyProfiles(null, CancellationToken.None);   // perform pre-cache API call of templates, don't wait on it
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
}