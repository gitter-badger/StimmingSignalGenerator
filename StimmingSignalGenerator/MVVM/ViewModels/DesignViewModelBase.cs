﻿using Splat;
using StimmingSignalGenerator.Generators;
using StimmingSignalGenerator.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StimmingSignalGenerator.MVVM.ViewModels
{
   public abstract class DesignViewModelBase
   {
      protected static readonly Random random = new Random();
      protected static bool RandomBool(int percentChange) => RandomHelper.RandomBool(percentChange);
      protected static T GetRandomEnum<T>() where T : Enum => RandomHelper.GetRandomEnum<T>();
      protected static void PrepareAppState() => PrepareAppState(GetRandomEnum<GeneratorModeType>());
      protected static void PrepareAppState(GeneratorModeType generatorModeType)
      {
         Locator.CurrentMutable.RegisterConstant(
            new AppState
            {
               GeneratorMode = generatorModeType,
               IsPlotEnable = true
            });
      }
   }
}
