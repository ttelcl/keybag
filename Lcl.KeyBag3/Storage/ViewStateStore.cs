/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lcl.KeyBag3.Utilities;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lcl.KeyBag3.Storage;

/// <summary>
/// Manages a view state and its storage. The view state is a
/// JObject. This class is used for both the view state of each
/// keybag as well as the app itself. As such it is agnostic of
/// its actual content beyond the JObject semantics.
/// </summary>
public class ViewStateStore
{
  /// <summary>
  /// Create a new ViewStateStore
  /// </summary>
  public ViewStateStore(
    string storeFile)
  {
    StoreFile = storeFile;
    ViewState = [];
    Reload();
  }

  /// <summary>
  /// The file where the view state is stored.
  /// </summary>
  public string StoreFile { get; }

  /// <summary>
  /// The raw view state object.
  /// </summary>
  public JObject ViewState { get; private set; }

  /// <summary>
  /// Create a new view on the view state
  /// (tracking changes in <see cref="ViewState"/> even
  /// if that is replaced)
  /// </summary>
  /// <returns></returns>
  public JObjectViewEx CreateView()
  {
    return new JObjectViewEx(() => ViewState);
  }

  /// <summary>
  /// Get or set a value in the view state, using
  /// <see cref="JObject"/> semantics (i.e. null if not found).
  /// </summary>
  public JToken? this[string key]
  {
    get => ViewState[key];
    set => ViewState[key] = value;
  }

  /// <summary>
  /// Reload the state from the store file.
  /// </summary>
  public void Reload()
  {
    var json =
      File.Exists(StoreFile)
      ? File.ReadAllText(StoreFile)
      : "";
    var state =
      String.IsNullOrEmpty(json)
      ? new JObject()
      : JObject.Parse(json);
    ViewState = state;
    if(!File.Exists(StoreFile))
    {
      Save();
    }
  }

  /// <summary>
  /// Save the current state to the store file.
  /// </summary>
  public void Save()
  {
    using(var trx = new FileWriteTransaction(StoreFile))
    {
      using(var writer = new StreamWriter(trx.Target))
      {
        writer.WriteLine(
          JsonConvert.SerializeObject(ViewState, Formatting.Indented));
      }
      trx.Commit();
    }
  }

}
