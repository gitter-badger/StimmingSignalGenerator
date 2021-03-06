﻿using NAudio.Wave;
using StimmingSignalGenerator.NAudio;
using StimmingSignalGenerator.MVVM.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace StimmingSignalGenerator.POCOs
{
   public class BasicSignal
   {
      public BasicSignalType Type { get; set; }
      public ControlSlider Frequency { get; set; }
      public ControlSlider PhaseShift { get; set; }
      public ControlSlider Volume { get; set; }
      public ControlSlider ZeroCrossingPosition { get; set; }
      public List<BasicSignal> AMSignals { get; set; }
      public List<BasicSignal> FMSignals { get; set; }
      public List<BasicSignal> PMSignals { get; set; }
      public string FrequencySyncFrom { get; set; }
   }
}
