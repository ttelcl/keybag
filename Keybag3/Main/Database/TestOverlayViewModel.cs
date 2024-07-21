/*
 * (c) 2024  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Keybag3.WpfUtilities;

namespace Keybag3.Main.Database;

public class TestOverlayViewModel: ViewModelBase
{
  public TestOverlayViewModel(
    ISupportsOverlay host)
  {
    Host = host;
  }

  public ISupportsOverlay Host { get; }

}
