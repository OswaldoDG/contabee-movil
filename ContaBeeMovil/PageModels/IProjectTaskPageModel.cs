using CommunityToolkit.Mvvm.Input;
using ContaBeeMovil.Models;

namespace ContaBeeMovil.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}