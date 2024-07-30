/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Keybag3.Main.Database;
using Keybag3.Main.KeybagContent;

namespace Keybag3.Main;

// Collection of interfaces for objects that know how to reach
// the main application components

public interface IKnowAppModel
{
  MainViewModel AppModel { get; }
}

public interface IKnowDbModel: IKnowAppModel
{
  KeybagDbViewModel DbModel { get; }
}

public interface IKnowSetModel: IKnowDbModel
{
  KeybagSetViewModel SetModel { get; }
}

public interface IKnowKeybagModel: IKnowSetModel
{
  KeybagViewModel KeybagModel { get; }
}

