using CommunityToolkit.Mvvm.Input;
using ImageGen.Models;

namespace ImageGen.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}