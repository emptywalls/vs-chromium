﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using VsChromiumCore.Processes;

namespace VsChromiumPackage.ServerProxy {
  [Export(typeof(IServerProcessLauncher))]
  public class ServerProcessLauncher : IServerProcessLauncher {
    private const string _proxyServerName = "VsChromiumHost.exe";
    private const string _serverName = "VsChromiumServer.exe";

    private readonly IProcessCreator _processCreator;
    private readonly object _serverProcessLock = new object();
    private ProcessProxy _serverProcess;

    [ImportingConstructor]
    public ServerProcessLauncher(IProcessCreator processCreator) {
      _processCreator = processCreator;
    }

    public void CreateProxy(Func<IEnumerable<string>> preCreate, Action<ProcessProxy> postCreate) {
      if (_serverProcess == null) {
        lock (_serverProcessLock) {
          if (_serverProcess == null) {
            CreateServerProcessWorker(preCreate, postCreate);
          }
        }
      }
    }

    public void Dispose() {
      if (_serverProcess != null) {
        _serverProcess.Dispose();
      }
    }

    private void CreateServerProcessWorker(Func<IEnumerable<string>> preCreate, Action<ProcessProxy> postCreate) {
      var path = GetProcessPath();
      var realServerName = Path.Combine(Path.GetDirectoryName(path), _serverName);

      var arguments = new List<string>();
      arguments.Add(realServerName);
      arguments.AddRange(preCreate());
#if PROFILE_SERVER
      arguments.Add("/profile-server");
#endif

      var argumentLine = arguments.Aggregate("", (x, v) => x + QuoteArgument(v) + " ");
      _serverProcess = _processCreator.CreateProcess(path, argumentLine,
                                                     CreateProcessOptions.RedirectStdio | CreateProcessOptions.AttachDebugger |
                                                     CreateProcessOptions.BreakAwayFromJob);

      postCreate(_serverProcess);
    }

    private string QuoteArgument(string argument) {
      const char quote = '\"';
      bool hasQuotes = argument.Length >= 2 && argument.First() == quote && argument.Last() == quote;
      if (hasQuotes)
        return argument;

      return quote + argument + quote;
    }

    private string GetProcessPath() {
      var result = GetCandidateProcessPaths()
        .Where(x => File.Exists(x))
        .OrderByDescending(x => File.GetLastWriteTimeUtc(x))
        .First();

      return result;
    }

    private IEnumerable<string> GetCandidateProcessPaths() {
      var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      yield return Path.Combine(folder, _proxyServerName);

      var serverFolder = Path.Combine(folder, "..");
      serverFolder = Path.Combine(serverFolder, "..");
      serverFolder = Path.Combine(serverFolder, _serverName);

      yield return Path.Combine(serverFolder, "bin\\Debug");
      yield return Path.Combine(serverFolder, "bin\\Release");
    }
  }
}