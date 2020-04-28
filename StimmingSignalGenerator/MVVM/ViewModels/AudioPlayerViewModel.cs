﻿using NAudio.CoreAudioApi;
using NAudio.Wave;
using ReactiveUI;
using StimmingSignalGenerator.SignalGenerator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;

namespace StimmingSignalGenerator.MVVM.ViewModels
{
   public class AudioPlayerViewModel : ViewModelBase, IDisposable
   {
      public MMDevice[] AudioDevices => audioPlayer.AudioDevices;
      public MMDevice AudioDevice
      {
         get => audioPlayer.AudioDevice;
         set
         {
            if (audioPlayer.AudioDevice == value) return;
            this.RaisePropertyChanging(nameof(AudioDevice));
            audioPlayer.AudioDevice = value;
            this.RaisePropertyChanged(nameof(AudioDevice));
         }
      }
      public ReactiveCommand<Unit, Unit> PlayCommand { get; }
      public ReactiveCommand<Unit, Unit> StopCommand { get; }

      private readonly AudioPlayer audioPlayer;
      
      public AudioPlayerViewModel(ISampleProvider sampleProvider)
      {
         audioPlayer = new AudioPlayer(sampleProvider);
         PlayCommand = ReactiveCommand.Create(() => Play());
         StopCommand = ReactiveCommand.Create(() => Stop());
      }

      public void Play()
      {
         audioPlayer.Play();
      }

      public void Stop()
      {
         audioPlayer.Stop();
      }

      private bool disposedValue;
      protected virtual void Dispose(bool disposing)
      {
         if (!disposedValue)
         {
            if (disposing)
            {
               // dispose managed state (managed objects)
               audioPlayer.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // set large fields to null
            disposedValue = true;
         }
      }

      // // override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
      // ~AudioPlayerViewModel()
      // {
      //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      //     Dispose(disposing: false);
      // }

      public void Dispose()
      {
         // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
         Dispose(disposing: true);
         GC.SuppressFinalize(this);
      }
   }
}
