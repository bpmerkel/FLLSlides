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

    /// <summary>
    /// Gets or sets the Blazor download file service.
    /// </summary>
    [Inject] private IBlazorDownloadFileService BlazorDownloadFileService { get; set; }

    /// <summary>
    /// Gets or sets the template details.
    /// </summary>
    private TemplateDetails Template { get; set; } = new TemplateDetails();

    /// <summary>
    /// Gets or sets the teams.
    /// </summary>
    private List<Team> Teams { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the schedule is being generated.
    /// </summary>
    private bool Generating { get; set; } = false;

    private string GetDefaultTeamText()
    {
        var teamsText = Teams.Count > 0
            ? string.Join(Environment.NewLine, Teams.Where(t => t.Number > 0).Select(t => $"{t.Number:0000}, {t.Name}"))
            : string.Join(Environment.NewLine, Enumerable.Range(1001, 24).Select(i => $"{i:0000}, team {i:0000}"));
        DoSetTeams(teamsText);
        return teamsText;
    }

    private void DoSetTeams(string teamsText)
    {
        Teams = teamsText
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Split(",;\t ".ToCharArray(), 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(pair => new Team { Number = Convert.ToInt32(pair[0]), Name = pair[1] })
            .ToList();
        Teams.Insert(0, OtherTeam);
    }

    /// <summary>
    /// Stores the prior team selections.
    /// </summary>
    private readonly Dictionary<string, Team> prior = [];

    /// <summary>
    /// Handles the template selection.
    /// </summary>
    /// <param name="template">The selected template.</param>
    private void DoTemplateSelected(TemplateDetails template)
    {
        if (Template != null && template != null && Template.Name != template.Name)
        {
            // capture values of selected fields
            foreach (var kvp in autocompletes)
            {
                prior[kvp.Key] = kvp.Value.Value;
            }
            Template = template;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Dictionary to store autocomplete components for teams.
    /// </summary>
    private readonly Dictionary<string, MudAutocomplete<Team>> autocompletes = [];

    /// <summary>
    /// Dictionary to store field substitutions.
    /// </summary>
    private readonly Dictionary<string, string> subs = [];

    /// <summary>
    /// Gets the default value for a specific field.
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <returns>The default team value.</returns>
    private Team GetDefaultValue(string field) => prior != null && prior.TryGetValue(field, out Team team)
        ? team
        : null;

    /// <summary>
    /// Gets the other default value for a specific field.
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <returns>The default string value.</returns>
    private string GetOtherDefaultValue(string field) => subs.TryGetValue(field, out string sub)
        ? sub
        : null;

    /// <summary>
    /// Handles the selection of a team for a specific field.
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <param name="team">The selected team.</param>
    private void DoFieldValueSelected(string field, Team team)
    {
        prior[field] = team;
        if (team.Number != 0)
        {
            subs[field] = $"{team.Name} ({team.Number})";
        }
    }

    /// <summary>
    /// Handles the selection of a value for a specific field.
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <param name="value">The selected value.</param>
    private void DoOtherFieldValueEntered(string field, string value)
    {
        subs[field] = value.Trim();
    }

    /// <summary>
    /// Identifies teams based on the input value.
    /// </summary>
    /// <param name="value">The input value.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A list of identified teams.</returns>
    private async Task<IEnumerable<Team>> IdentifyTeams(string value, CancellationToken token)
    {
        return await Task.FromResult(Teams
            .Where(t => value == null || t.Name.Contains(value, StringComparison.InvariantCultureIgnoreCase))
            .AsEnumerable());
    }

    private static readonly Team OtherTeam = new() { Name = "Other (enter value to the right) --->", Number = 0 };

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
                Name = Template.Name,
                TemplateDetails = Template,
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

    /// <summary>
    /// Stores the template response.
    /// </summary>
    private TemplateResponse templates;

    /// <summary>
    /// Identifies profiles based on the input value.
    /// </summary>
    /// <param name="value">The input value.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A list of identified profiles.</returns>
    private async Task<IEnumerable<TemplateDetails>> IdentifyProfiles(string value, CancellationToken token)
    {
        if (templates == null)
        {
            var httpClient = ClientFactory.CreateClient("API");
            using var response = await httpClient.PostAsJsonAsync("api/GetTemplateDetails", new TemplateRequest { Name = "Web UI" }, token);
            if (response.IsSuccessStatusCode)
            {
                templates = await response.Content.ReadFromJsonAsync<TemplateResponse>(token);
                if (templates.Templates.Length != 0 && Template.Name == null)
                {
                    Template = templates.Templates.First();
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
    /// <param name="firstRender">Indicates whether this is the first render.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
