using CommunityToolkit.Mvvm.Input;
using KaamLink.Models;

namespace KaamLink.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}