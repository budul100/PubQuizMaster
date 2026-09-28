using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Standings;
using PubQuizMaster.Services.Common;
using PubQuizMaster.Services.Standings;

namespace PubQuizMaster.Web.Components.Common
{
    /// <summary>
    /// Inline rename of a team, the one editor for the live night and the team registry.
    /// A name that another team already carries offers a merge into that team instead.
    /// Renames and merges are global: they change the team in every quiz night.
    /// </summary>
    public partial class TeamNameEditor
    {
        #region Private Fields

        private Team? conflict;
        private string editValue = string.Empty;
        private ElementReference inputRef;
        private bool isBusy;
        private bool isEditing;
        private bool shouldFocus;

        #endregion Private Fields

        #region Public Properties

        /// <summary>Shows the name without the edit button.</summary>
        [Parameter] public bool IsReadOnly { get; set; }

        [Parameter, EditorRequired] public string Name { get; set; } = string.Empty;

        /// <summary>Extra classes for the name text, e.g. muted or struck through.</summary>
        [Parameter] public string? NameClass { get; set; }

        /// <summary>Raised after the team was merged into another one. The source team no longer exists.</summary>
        [Parameter] public EventCallback<TeamMerge> OnMerged { get; set; }

        /// <summary>Raised after the team was renamed.</summary>
        [Parameter] public EventCallback<Team> OnRenamed { get; set; }

        [Parameter, EditorRequired] public Guid TeamId { get; set; }

        #endregion Public Properties

        #region Private Properties

        [Inject] private TeamService TeamService { get; set; } = null!;

        [Inject] private ToastService ToastService { get; set; } = null!;

        #endregion Private Properties

        #region Protected Methods

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!shouldFocus) return;

            shouldFocus = false;

            try
            {
                await inputRef.FocusAsync();
            }
            catch
            {
                // Element detached or not yet focusable
            }
        }

        #endregion Protected Methods

        #region Private Methods

        private void CancelEdit()
        {
            isEditing = false;
            editValue = string.Empty;
        }

        private void CancelMerge()
        {
            conflict = null;
            shouldFocus = isEditing;
        }

        private async Task HandleKeyDownAsync(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
            {
                await SaveAsync();
            }
            else if (e.Key == "Escape")
            {
                CancelEdit();
            }
        }

        private async Task MergeAsync()
        {
            if (conflict == null || isBusy) return;

            isBusy = true;

            try
            {
                var result = await TeamService.MergeTeamsAsync(
                    sourceTeamId: TeamId,
                    targetTeamId: conflict.Id);

                ToastService.ShowSuccess($"Merged '{result.SourceName}' into '{result.TargetName}'.");

                conflict = null;
                CancelEdit();

                await OnMerged.InvokeAsync(result);
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Merge failed: {ex.Message}");
            }
            finally
            {
                isBusy = false;
            }
        }

        private async Task SaveAsync()
        {
            if (isBusy) return;

            var newName = editValue.Trim();

            if (newName.Length == 0 || newName == Name)
            {
                CancelEdit();
                return;
            }

            isBusy = true;

            try
            {
                conflict = await TeamService.FindConflictingTeamAsync(
                    teamId: TeamId,
                    name: newName);
                if (conflict != null) return;

                var team = await TeamService.RenameTeamAsync(
                    teamId: TeamId,
                    newName: newName);
                ToastService.ShowSuccess($"Team renamed to '{team.Name}'.");

                CancelEdit();

                await OnRenamed.InvokeAsync(team);
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
            finally
            {
                isBusy = false;
            }
        }

        private Task StartEditAsync()
        {
            editValue = Name;
            isEditing = true;
            shouldFocus = true;
            return Task.CompletedTask;
        }

        #endregion Private Methods
    }
}