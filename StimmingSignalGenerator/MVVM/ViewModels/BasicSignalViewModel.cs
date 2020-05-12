﻿using Avalonia.Media;
using DynamicData;
using NAudio.Wave.SampleProviders;
using ReactiveUI;
using StimmingSignalGenerator.Generators;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;

namespace StimmingSignalGenerator.MVVM.ViewModels
{
   class BasicSignalViewModel : ViewModelBase, IDisposable
   {
      public int Id { get; internal set; }
      private string name = "SignalGenerator";
      public string Name { get => name; set => this.RaiseAndSetIfChanged(ref name, value); }
      public BasicSignal BasicSignal { get; }
      public ControlSliderViewModel FreqControlSliderViewModel { get; }
      public ControlSliderViewModel VolControlSliderViewModel { get; }
      public ControlSliderViewModel ZCPosControlSliderViewModel { get; }

      public bool IsExpanded { get => isExpanded; set => this.RaiseAndSetIfChanged(ref isExpanded, value); }
      private bool isExpanded;
      public bool IsAMExpanded { get => isAMExpanded; set => this.RaiseAndSetIfChanged(ref isAMExpanded, value); }
      private bool isAMExpanded;
      public bool IsFMExpanded { get => isFMExpanded; set => this.RaiseAndSetIfChanged(ref isFMExpanded, value); }
      private bool isFMExpanded;

      private readonly ReadOnlyObservableCollection<BasicSignalViewModel> amSignalVMs;
      public ReadOnlyObservableCollection<BasicSignalViewModel> AMSignalVMs => amSignalVMs;
      private SourceCache<BasicSignalViewModel, int> AMSignalVMsSourceCache { get; }
      public ReactiveCommand<Unit, Unit> AddAMCommand { get; }
      public ReactiveCommand<BasicSignalViewModel, Unit> RemoveAMCommand { get; }


      private readonly ReadOnlyObservableCollection<BasicSignalViewModel> fmSignalVMs;
      public ReadOnlyObservableCollection<BasicSignalViewModel> FMSignalVMs => fmSignalVMs;
      private SourceCache<BasicSignalViewModel, int> FMSignalVMsSourceCache { get; }
      public ReactiveCommand<Unit, Unit> AddFMCommand { get; }
      public ReactiveCommand<BasicSignalViewModel, Unit> RemoveFMCommand { get; }

      public Brush BGColor { get; }

      public static BasicSignalViewModel FromPOCO(POCOs.BasicSignal poco)
      {
         var basicSignalVM = new BasicSignalViewModel(
            ControlSliderViewModel.FromPOCO(poco.Frequency),
            ControlSliderViewModel.FromPOCO(poco.Volume),
            ControlSliderViewModel.FromPOCO(poco.ZeroCrossingPosition))
         {
            SignalType = poco.Type
         };

         foreach (var am in poco.AMSignals)
         {
            var amVM = FromPOCO(am);
            amVM.Id = basicSignalVM.GetNextId(basicSignalVM.AMSignalVMsSourceCache);
            amVM.Name = basicSignalVM.CreateAMName();
            basicSignalVM.AddAM(amVM);
         }
         foreach (var fm in poco.FMSignals)
         {
            var fmVM = FromPOCO(fm);
            fmVM.Id = basicSignalVM.GetNextId(basicSignalVM.FMSignalVMsSourceCache);
            fmVM.Name = basicSignalVM.CreateFMName();
            basicSignalVM.AddAM(fmVM);
         }

         return basicSignalVM;
      }
      public POCOs.BasicSignal ToPOCO() =>
         new POCOs.BasicSignal()
         {
            Type = BasicSignal.Type,
            Frequency = FreqControlSliderViewModel.ToPOCO(),
            Volume = VolControlSliderViewModel.ToPOCO(),
            ZeroCrossingPosition = ZCPosControlSliderViewModel.ToPOCO(),
            AMSignals = AMSignalVMs.Select(x => x.ToPOCO()).ToList(),
            FMSignals = FMSignalVMs.Select(x => x.ToPOCO()).ToList()
         };

      public BasicSignalViewModel()
         : this(ControlSliderViewModel.BasicSignalFreq)
      {
      }
      public BasicSignalViewModel(
         ControlSliderViewModel freqControlSliderViewModel)
         : this(freqControlSliderViewModel, ControlSliderViewModel.BasicVol)
      {
      }
      public BasicSignalViewModel(
         ControlSliderViewModel freqControlSliderViewModel,
         ControlSliderViewModel volControlSliderViewModel)
         : this(freqControlSliderViewModel, volControlSliderViewModel, ControlSliderViewModel.Vol(0.5)) { }

      public BasicSignalViewModel(
         ControlSliderViewModel freqControlSliderViewModel,
         ControlSliderViewModel volControlSliderViewModel,
         ControlSliderViewModel zcPosControlSliderViewModel
         )
      {
         BGColor = GetRandomBrush();
         BasicSignal = new BasicSignal();

         FreqControlSliderViewModel = freqControlSliderViewModel;
         VolControlSliderViewModel = volControlSliderViewModel;
         ZCPosControlSliderViewModel = zcPosControlSliderViewModel;

         FreqControlSliderViewModel
            .ObservableForProperty(x => x.Value, skipInitial: false)
            .Subscribe(x => Frequency = x.Value)
            .DisposeWith(Disposables);
         VolControlSliderViewModel
            .ObservableForProperty(x => x.Value, skipInitial: false)
            .Subscribe(x => Volume = x.Value)
            .DisposeWith(Disposables);
         ZCPosControlSliderViewModel
            .ObservableForProperty(x => x.Value, skipInitial: false)
            .Subscribe(x => ZeroCrossingPosition = x.Value)
            .DisposeWith(Disposables);

         SignalType = BasicSignalType.Sin;

         AMSignalVMsSourceCache =
            new SourceCache<BasicSignalViewModel, int>(x => x.Id)
            .DisposeWith(Disposables);
         AMSignalVMsSourceCache.Connect()
            .OnItemAdded(vm => BasicSignal.AddAMSignal(vm.BasicSignal))
            .OnItemRemoved(vm =>
            {
               BasicSignal.RemoveAMSignal(vm.BasicSignal);
               vm.Dispose();
            })
            .ObserveOn(RxApp.MainThreadScheduler) // Make sure this is only right before the Bind()
            .Bind(out amSignalVMs)
            .Subscribe()
            .DisposeWith(Disposables);
         AddAMCommand = ReactiveCommand.Create(
            () => AddAM())
            .DisposeWith(Disposables);
         RemoveAMCommand = ReactiveCommand.Create<BasicSignalViewModel>(
            vm => AMSignalVMsSourceCache.Remove(vm))
            .DisposeWith(Disposables);


         FMSignalVMsSourceCache =
            new SourceCache<BasicSignalViewModel, int>(x => x.Id)
            .DisposeWith(Disposables);
         FMSignalVMsSourceCache.Connect()
            .OnItemAdded(vm => BasicSignal.AddFMSignal(vm.BasicSignal))
            .OnItemRemoved(vm =>
            {
               BasicSignal.RemoveFMSignal(vm.BasicSignal);
               vm.Dispose();
            })
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out fmSignalVMs)
            .Subscribe()
            .DisposeWith(Disposables);
         AddFMCommand = ReactiveCommand.Create(
            () => AddFM())
            .DisposeWith(Disposables);
         RemoveFMCommand = ReactiveCommand.Create<BasicSignalViewModel>(
            vm => FMSignalVMsSourceCache.Remove(vm))
            .DisposeWith(Disposables);

         // HACK Expander IsExpanded is set somewhere from internal avalonia uncontrollable
         this.WhenAnyValue(x => x.IsAMExpanded)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Sample(TimeSpan.FromMilliseconds(30),RxApp.TaskpoolScheduler)
            .Where(x => x).Take(1)
            .SubscribeOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => IsAMExpanded = AMSignalVMs.Count > 0)
            .DisposeWith(Disposables);

         this.WhenAnyValue(x => x.IsFMExpanded)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Sample(TimeSpan.FromMilliseconds(30), RxApp.TaskpoolScheduler)
            .Where(x => x).Take(1)
            .SubscribeOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => IsFMExpanded = FMSignalVMs.Count > 0)
            .DisposeWith(Disposables);
      }

      private string CreateFMName() => $"FMSignal{GetNextId(FMSignalVMsSourceCache) + 1}";
      private void AddFM() => AddFM(CreateFMVM(CreateFMName()));
      private void AddFM(BasicSignalViewModel FMVM)
      {
         FMSignalVMsSourceCache.AddOrUpdate(FMVM);
      }
      private string CreateAMName() => $"AMSignal{GetNextId(AMSignalVMsSourceCache) + 1}";
      private void AddAM() => AddAM(CreateAMVM(CreateAMName()));
      private void AddAM(BasicSignalViewModel AMVM)
      {
         AMSignalVMsSourceCache.AddOrUpdate(AMVM);
      }

      private BasicSignalType signalType;
      public BasicSignalType SignalType
      {
         get => signalType;
         set
         {
            this.RaiseAndSetIfChanged(ref signalType, value);
            BasicSignal.Type = signalType;
         }
      }
      public double Frequency
      {
         get => FreqControlSliderViewModel.Value;
         set
         {
            if (BasicSignal.Frequency == value) return;
            this.RaisePropertyChanging(nameof(Frequency));
            FreqControlSliderViewModel.Value = value;
            BasicSignal.Frequency = value;
            this.RaisePropertyChanged(nameof(Frequency));
         }
      }
      public double Volume
      {
         get => VolControlSliderViewModel.Value;
         set
         {
            if (BasicSignal.Gain == value) return;
            this.RaisePropertyChanging(nameof(Volume));
            VolControlSliderViewModel.Value = value;
            BasicSignal.Gain = value;
            this.RaisePropertyChanged(nameof(Volume));
         }
      }

      public double ZeroCrossingPosition
      {
         get => ZCPosControlSliderViewModel.Value;
         set
         {
            if (BasicSignal.ZeroCrossingPosition == value) return;
            this.RaisePropertyChanging(nameof(ZeroCrossingPosition));
            ZCPosControlSliderViewModel.Value = value;
            BasicSignal.ZeroCrossingPosition = value;
            this.RaisePropertyChanged(nameof(ZeroCrossingPosition));
         }
      }

      private static readonly Random rand = new Random();
      private Brush GetRandomBrush()
      {
         var (r, g, b) = Helper.ColorHelper.HsvToRgb(rand.Next(0, 360), 1, 1);
         return new SolidColorBrush(Color.FromArgb(60, r, g, b));
      }

      private BasicSignalViewModel CreateAMVM(string name, double volume = 0) =>
         new BasicSignalViewModel(
            ControlSliderViewModel.ModulationSignalFreq)
         { Name = name, Id = GetNextId(AMSignalVMsSourceCache), Volume = 0 }
         .DisposeWith(Disposables);

      private BasicSignalViewModel CreateFMVM(string name, double volume = 0) =>
         new BasicSignalViewModel(
            ControlSliderViewModel.ModulationSignalFreq,
            new ControlSliderViewModel(0, 0, 100, 1, 1, 5))
         { Name = name, Id = GetNextId(FMSignalVMsSourceCache), Volume = 0 }
         .DisposeWith(Disposables);

      private int GetNextId(SourceCache<BasicSignalViewModel, int> SourceCache) =>
         SourceCache.Count == 0 ?
            0 : SourceCache.Keys.Max() + 1;

      private CompositeDisposable Disposables { get; } = new CompositeDisposable();
      private bool disposedValue;
      protected virtual void Dispose(bool disposing)
      {
         if (!disposedValue)
         {
            if (disposing)
            {
               // dispose managed state (managed objects)
               Disposables?.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // set large fields to null
            disposedValue = true;
         }
      }

      // // override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
      // ~BasicSignalViewModel()
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
