using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoutubeDownloader.Core.Downloading;
using YoutubeDownloader.Framework;
using YoutubeDownloader.Services;
using YoutubeDownloader.Utils;
using YoutubeDownloader.Utils.Extensions;
using YoutubeDownloader.ViewModels.Components;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader.ViewModels.Dialogs;

public partial class DownloadMultipleSetupViewModel(
    ViewModelManager viewModelManager,
    DialogManager dialogManager,
    SettingsService settingsService
) : DialogViewModelBase<IReadOnlyList<DownloadViewModel>>
{
    #region Properties

    public ObservableCollection<IVideo> SelectedVideos { get; } = [];

    public IReadOnlyList<Container> AvailableContainers { get; } =
        [Container.Mp4, Container.WebM, Container.Mp3, new("ogg")];

    public IReadOnlyList<VideoQualityPreference> AvailableVideoQualityPreferences { get; } =
        Enum.GetValues<VideoQualityPreference>();

    #endregion

    #region Fields

    [ObservableProperty]
    private IReadOnlyList<IVideo>? _availableVideos;

    [ObservableProperty]
    private Container _selectedContainer = Container.Mp4;

    [ObservableProperty]
    private VideoQualityPreference _selectedVideoQualityPreference = VideoQualityPreference.Highest;

    [ObservableProperty]
    private string? _title;

    #endregion

    #region Methods

    [RelayCommand]
    private void Initialize()
    {
        SelectedContainer = settingsService.LastContainer;
        SelectedVideoQualityPreference = settingsService.LastVideoQualityPreference;
        SelectedVideos.CollectionChanged += (_, _) => ConfirmCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task CopyTitleAsync()
    {
        if (Application.Current?.ApplicationLifetime?.TryGetTopLevel()?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(Title);
        }
    }

    private bool CanConfirm()
    {
        return SelectedVideos.Any();
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        string? dirPath = await dialogManager.PromptDirectoryPathAsync();
        if (string.IsNullOrWhiteSpace(dirPath))
        {
            return;
        }

        List<DownloadViewModel> downloads = new();
        for (int i = 0; i < SelectedVideos.Count; i++)
        {
            IVideo video = SelectedVideos[i];

            string baseFilePath = Path.Combine(
                dirPath,
                FileNameTemplate.Apply(
                    settingsService.FileNameTemplate,
                    video,
                    SelectedContainer,
                    (i + 1).ToString().PadLeft(SelectedVideos.Count.ToString().Length, '0')
                )
            );

            if (settingsService.ShouldSkipExistingFiles && File.Exists(baseFilePath))
            {
                continue;
            }

            string filePath = PathEx.EnsureUniquePath(baseFilePath);

            // Download does not start immediately, so lock in the file path to avoid conflicts
            DirectoryEx.CreateDirectoryForFile(filePath);
            await File.WriteAllBytesAsync(filePath, []);

            downloads.Add(
                viewModelManager.CreateDownloadViewModel(
                    video,
                    new VideoDownloadPreference(SelectedContainer, SelectedVideoQualityPreference),
                    filePath
                )
            );
        }

        settingsService.LastContainer = SelectedContainer;
        settingsService.LastVideoQualityPreference = SelectedVideoQualityPreference;

        Close(downloads);
    }

    #endregion
}
