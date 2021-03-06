﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StimmingSignalGenerator.POCOs
{
   public class Track
   {
      public string Name { get; set; }
      public List<MultiSignal> MultiSignals { get; set; }
      public List<ControlSlider> Volumes { get; set; }
      public double TimeSpanSecond { get; set; }
   }
}
