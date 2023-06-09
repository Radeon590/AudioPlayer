﻿using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace AudioPlayer
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<string> _playlist = new ObservableCollection<string>();
        private List<string> _playlistPaths = new List<string>();
        private MediaPlayer _player = new MediaPlayer();
        CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();

        public MainWindow()
        {
            InitializeComponent();
            //
            PlaylistBox.ItemsSource = _playlist;
        }

        private void AddTrack(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.DefaultExt = ".mp3";
            dialog.Filter = "MP3 files (*.mp3)|*.mp3|WAV Files (*.wav)|*.wav";
            dialog.Multiselect = true;
            if (dialog.ShowDialog() == true)
            {
                AddTrack(dialog.FileNames);
            }
        }

        private void AddTrack(string[] fileNames)
        {
            foreach (var file in fileNames)
            {
                var filePathSplitted = file.Split('\\');
                _playlistPaths.Add(file);
                _playlist.Add(filePathSplitted[filePathSplitted.Length - 1].Split('.')[0]);
            }
            PlaylistBox.SelectedIndex = _playlist.Count - 1;
        }

        private void OpenPlaylist(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.Multiselect = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                _cancelTokenSource.Cancel();
                _playlist.Clear();
                _playlistPaths.Clear();
                foreach (var directory in dialog.FileNames)
                {
                    AddTrack(new DirectoryInfo(directory).GetFiles().Where(f => f.FullName.EndsWith(".mp3") || f.FullName.EndsWith(".wav"))
                        .Select(f => f.FullName).ToArray());
                }
            }
        }

        private void SavePlaylist(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.Multiselect = false;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                for(int i = 0; i < _playlistPaths.Count; i++)
                {
                    string newPath = $"{dialog.FileName}\\{_playlist[i]}.{_playlistPaths[i].Split(".")[1]}";
                    if(!File.Exists(newPath))
                        File.Copy(_playlistPaths[i], newPath);
                }
            }
        }

        private void ChangeVolume(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _player.Volume = e.NewValue;
        }

        private void ChangePosition(object sender, DragCompletedEventArgs e)
        {
            _player.Position = TimeSpan.FromSeconds(((Slider)sender).Value);
            StartProgressTask();
        }

        private void CancelProgressTask(object sender, DragStartedEventArgs e)
        {
            _cancelTokenSource.Cancel();
        }

        private void SelectTrack(object sender, SelectionChangedEventArgs e)
        {
            if(PlaylistBox.SelectedIndex >= 0 && PlaylistBox.SelectedIndex < _playlistPaths.Count && _playlistPaths.Count > 0)
            {
                PlayTrack(_playlistPaths[PlaylistBox.SelectedIndex]);
            }
        }

        private void PlayTrack(string trackPath)
        {
            _player.Open(new Uri(trackPath));
            _player.MediaOpened += new EventHandler(SetupPlayer);
        }

        private void SetupPlayer(object? sender, EventArgs e)
        {
            ProgressSlider.Maximum = _player.NaturalDuration.TimeSpan.TotalSeconds;
            ProgressSlider.Value = ProgressSlider.Minimum;
            StartProgressTask();
            _player.Play();
        }

        private void StartProgressTask()
        {
            _cancelTokenSource.Cancel();
            _cancelTokenSource = new CancellationTokenSource();
            Task progressTask = new Task(() => Progress(_cancelTokenSource.Token));
            progressTask.Start();
        }

        private void Progress(CancellationToken token)
        {
            while (true)
            {
                Dispatcher.Invoke(() => 
                {
                    ProgressSlider.Value = _player.Position.TotalSeconds;
                    if(ProgressSlider.Value == ProgressSlider.Maximum)
                    {
                        int index = _playlistPaths.IndexOf(_player.Source.LocalPath);
                        if(index == _playlistPaths.Count - 1)
                        {
                            index = 0;
                            if(_playlistPaths.Count == 1)
                            {
                                _player = new MediaPlayer();
                                _player.Volume = VolumeSlider.Value;
                                SelectTrack(null, null);
                                _cancelTokenSource.Cancel();
                                return;
                            }
                        }
                        else
                        {
                            index++;
                        }
                        PlaylistBox.SelectedIndex = index;
                        _cancelTokenSource.Cancel();
                    }
                });

                if (token.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}
