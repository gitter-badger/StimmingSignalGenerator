﻿using Avalonia;
using NAudio.Wave;
using ReactiveUI;
using Splat;
using StimmingSignalGenerator.FileService;
using StimmingSignalGenerator.Generators;
using StimmingSignalGenerator.MVVM.ViewModels.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StimmingSignalGenerator.Helper;
using System.Reactive.Linq;
using DynamicData;
using System.Collections.ObjectModel;

namespace StimmingSignalGenerator.MVVM.ViewModels
{
   public class DesignTrackViewModel : DesignViewModelBase
   {
      public static TrackViewModel MonoData => CreateTrackViewModel(GeneratorModeType.Mono);
      public static TrackViewModel StereoData => CreateTrackViewModel(GeneratorModeType.Stereo);
      static TrackViewModel CreateTrackViewModel(GeneratorModeType generatorModeType)
      {
         PrepareAppState();
         return new TrackViewModel { Name = "Track1", GeneratorMode = generatorModeType };
      }
   }
   public class TrackViewModel : ViewModelBase, INamable, ISignalTree
   {
      public AppState AppState { get; }
      public string Name { get => name; set => this.RaiseAndSetIfChanged(ref name, value); }
      public bool IsPlaying { get => isPlaying; set => this.RaiseAndSetIfChanged(ref isPlaying, value); }
      public bool IsSelected { get => isSelected; set => this.RaiseAndSetIfChanged(ref isSelected, value); }
      public float Progress { get => progress; set => this.RaiseAndSetIfChanged(ref progress, value); }
      public double TimeSpanSecond { get => timeSpanSecond; set => this.RaiseAndSetIfChanged(ref timeSpanSecond, Math.Round(value, 2)); }
      public List<MultiSignalViewModel> MultiSignalVMs { get; }
      public List<ControlSliderViewModel> VolVMs { get; }
      public GeneratorModeType GeneratorMode { get => generatorMode; set => this.RaiseAndSetIfChanged(ref generatorMode, value); }
      public IObservable<BasicSignalViewModel> ObservableBasicSignalViewModelsAdded
         => GeneratorMode switch
         {
            GeneratorModeType.Mono =>
               MultiSignalVMs[0].ObservableBasicSignalViewModelsAdded,
            GeneratorModeType.Stereo =>
               MultiSignalVMs[1].ObservableBasicSignalViewModelsAdded.Merge(
               MultiSignalVMs[2].ObservableBasicSignalViewModelsAdded),
            _ => throw new NotImplementedException()
         };
      public IObservable<BasicSignalViewModel> ObservableBasicSignalViewModelsRemoved
         => GeneratorMode switch
         {
            GeneratorModeType.Mono =>
               MultiSignalVMs[0].ObservableBasicSignalViewModelsRemoved,
            GeneratorModeType.Stereo =>
               MultiSignalVMs[1].ObservableBasicSignalViewModelsRemoved.Merge(
               MultiSignalVMs[2].ObservableBasicSignalViewModelsRemoved),
            _ => throw new NotImplementedException()
         };

      public ReadOnlyObservableCollection<BasicSignalViewModel> AllSubBasicSignalVMs => allSubBasicSignalVMs;
      public ISignalTree Parent => null;
      public ISampleProvider FinalSample => sample;

      private SourceList<BasicSignalViewModel> AllSubBasicSignalVMsSourceList { get; }
      private readonly ReadOnlyObservableCollection<BasicSignalViewModel> allSubBasicSignalVMs;
      private readonly SwitchingModeSampleProvider sample;
      private GeneratorModeType generatorMode;
      private string name;
      private bool isPlaying;
      private bool isSelected;
      private float progress;
      private double timeSpanSecond = 0;

      public static TrackViewModel FromPOCO(POCOs.Track poco)
      {
         var vm = new TrackViewModel(
            poco.MultiSignals.Select(x => MultiSignalViewModel.FromPOCO(x)).ToArray(),
            poco.Volumes.Select(x => ControlSliderViewModel.FromPOCO(x)).ToArray()
            );
         vm.name = poco.Name;
         vm.TimeSpanSecond = poco.TimeSpanSecond;
         return vm;
      }
      public POCOs.Track ToPOCO()
      {
         IEnumerable<POCOs.MultiSignal> signalPocos;
         IEnumerable<POCOs.ControlSlider> volPocos;
         switch (GeneratorMode)
         {
            case GeneratorModeType.Mono:
               signalPocos = MultiSignalVMs.Take(1).Select(vm => vm.ToPOCO());
               volPocos = VolVMs.Take(2).Select(vm => vm.ToPOCO());
               break;
            case GeneratorModeType.Stereo:
               signalPocos = MultiSignalVMs.Skip(1).Select(vm => vm.ToPOCO());
               volPocos = VolVMs.Skip(2).Take(1).Select(vm => vm.ToPOCO());
               break;
            default:
               throw new ApplicationException("Bad GeneratorMode");
         }
         return new POCOs.Track
         {
            Name = Name,
            MultiSignals = signalPocos.ToList(),
            Volumes = volPocos.ToList(),
            TimeSpanSecond = TimeSpanSecond
         };
      }
      public TrackViewModel() : this(
         new[] { new MultiSignalViewModel(), new MultiSignalViewModel(), new MultiSignalViewModel() },
         new[] { ControlSliderViewModel.BasicVol, ControlSliderViewModel.BasicVol, ControlSliderViewModel.BasicVol })
      { }
      public TrackViewModel(MultiSignalViewModel[] multiSignalVMs, ControlSliderViewModel[] controlSliderVMs)
      {
         AppState = Locator.Current.GetService<AppState>();

         sample = new SwitchingModeSampleProvider();

         VolVMs = new List<ControlSliderViewModel>();
         MultiSignalVMs = new List<MultiSignalViewModel>();

         AllSubBasicSignalVMsSourceList =
            new SourceList<BasicSignalViewModel>()
            .DisposeWith(Disposables);
         AllSubBasicSignalVMsSourceList.Connect()
            .ObserveOn(RxApp.MainThreadScheduler) // Make sure this is only right before the Bind()
            .Bind(out allSubBasicSignalVMs)
            .Subscribe()
            .DisposeWith(Disposables);

         SetupVolumeControlSlider(controlSliderVMs);
         SetupSwitchingModeSignal(multiSignalVMs);

         var GeneratorModeChangedDisposable = new CompositeDisposable().DisposeWith(Disposables);
         this.WhenAnyValue(x => x.GeneratorMode)
            .Subscribe(_ =>
            {
               sample.GeneratorMode = GeneratorMode;
               // clean and switch mode
               AllSubBasicSignalVMsSourceList.Clear();
               GeneratorModeChangedDisposable.Dispose();
               GeneratorModeChangedDisposable = new CompositeDisposable().DisposeWith(Disposables);
               // resub to new mode
               this.ObservableBasicSignalViewModelsAdded
                  .Subscribe(x => AllSubBasicSignalVMsSourceList.Add(x))
                  .DisposeWith(GeneratorModeChangedDisposable);
               this.ObservableBasicSignalViewModelsRemoved
                  .Subscribe(x => AllSubBasicSignalVMsSourceList.Remove(x))
                  .DisposeWith(GeneratorModeChangedDisposable);
            })
            .DisposeWith(Disposables);
         this.WhenAnyValue(x => x.IsPlaying)
            .Subscribe(_ => { if (!IsPlaying) Progress = 0; })
            .DisposeWith(Disposables);
         VolVMs[0].WhenAnyValue(vm => vm.Value)
            .Subscribe(m => sample.MonoLeftVolume = (float)m)
            .DisposeWith(Disposables);
         VolVMs[1].WhenAnyValue(vm => vm.Value)
            .Subscribe(m => sample.MonoRightVolume = (float)m)
            .DisposeWith(Disposables);
         VolVMs[2].WhenAnyValue(vm => vm.Value)
            .Subscribe(m => sample.StereoVolume = (float)m)
            .DisposeWith(Disposables);

      }

      private void SetupSwitchingModeSignal(MultiSignalViewModel[] multiSignalVMs)
      {
         var multiSignalVMsCount = multiSignalVMs.Count();
         switch (multiSignalVMsCount)
         {
            case 1:
               //mono
               GeneratorMode = GeneratorModeType.Mono;
               MultiSignalVMs.Clear();
               MultiSignalVMs.AddRange(new[]
               {
                  multiSignalVMs[0].DisposeWith(Disposables),
                  new MultiSignalViewModel().DisposeWith(Disposables),
                  new MultiSignalViewModel().DisposeWith(Disposables)
               });
               break;
            case 2:
               //stereo
               GeneratorMode = GeneratorModeType.Stereo;
               MultiSignalVMs.Clear();
               MultiSignalVMs.AddRange(new[]
               {
                  new MultiSignalViewModel().DisposeWith(Disposables),
                  multiSignalVMs[0].DisposeWith(Disposables),
                  multiSignalVMs[1].DisposeWith(Disposables),
               });
               break;
            case 3:
               //load all
               MultiSignalVMs.Clear();
               MultiSignalVMs.AddRange(new[]
               {
                  multiSignalVMs[0].DisposeWith(Disposables),
                  multiSignalVMs[1].DisposeWith(Disposables),
                  multiSignalVMs[2].DisposeWith(Disposables),
               });
               break;
            default:
               //somthing wrong
               throw new ApplicationException("somthing wrong in TrackViewModel.SetupMultiSignal(params MultiSignalViewModel[] multiSignalVMs)");
         }
         MultiSignalVMs[0].Name = "Signals";
         MultiSignalVMs[1].Name = "LSignals";
         MultiSignalVMs[2].Name = "RSignals";
         foreach (var item in MultiSignalVMs)
         {
            item.Parent = this;
         }
         sample.MonoSampleProvider = MultiSignalVMs.Take(1).Single().SampleSignal;
         sample.StereoSampleProviders = MultiSignalVMs.Skip(1).Select(x => x.SampleSignal);
      }

      private void SetupVolumeControlSlider(ControlSliderViewModel[] controlSliderVMs)
      {
         if (controlSliderVMs == null) controlSliderVMs = Array.Empty<ControlSliderViewModel>();
         VolVMs.Clear();
         switch (controlSliderVMs.Length)
         {
            case 2://mono
               VolVMs.AddRange(
                  new[] {
                     controlSliderVMs[0],
                     controlSliderVMs[1],
                     ControlSliderViewModel.BasicVol
                  });
               break;
            case 1://stereo
               VolVMs.AddRange(
                  new[] {
                     controlSliderVMs[0],
                     ControlSliderViewModel.BasicVol,
                     ControlSliderViewModel.BasicVol
                  });
               break;
            case 3://load all
               VolVMs.AddRange(controlSliderVMs);
               break;
            case 0:
               VolVMs.AddRange(
                  new[] {
                     ControlSliderViewModel.BasicVol,
                     ControlSliderViewModel.BasicVol,
                     ControlSliderViewModel.BasicVol
                  });
               break;
            default:
               //somthing wrong
               throw new ApplicationException("somthing wrong in TrackViewModel.SetVolumesFromPOCOs(POCOs.ControlSlider[] pocoVols)");
         }
      }

      public async Task CopyToClipboard()
      {
         var poco = this.ToPOCO();
         var json = JsonSerializer.Serialize(poco, new JsonSerializerOptions { WriteIndented = true });
         await Avalonia.Application.Current.Clipboard.SetTextAsync(json);
      }
      public static async Task<TrackViewModel> PasteFromClipboard()
      {
         var json = await Avalonia.Application.Current.Clipboard.GetTextAsync();
         if (string.IsNullOrWhiteSpace(json)) return null;
         try
         {
            var poco = JsonSerializer.Deserialize<POCOs.Track>(json);
            if (typeof(POCOs.Track).GetProperties().All(x => x.GetValue(poco).IsNullOrDefault())) return null;
            return TrackViewModel.FromPOCO(poco);
         }
         catch (JsonException)
         {
            return null;
         }
      }
   }
}
