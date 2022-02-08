// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Online.Spectator
{
    public abstract class SpectatorClient : Component, ISpectatorClient
    {
        /// <summary>
        /// The maximum milliseconds between frame bundle sends.
        /// </summary>
        public const double TIME_BETWEEN_SENDS = 200;

        /// <summary>
        /// Whether the <see cref="SpectatorClient"/> is currently connected.
        /// This is NOT thread safe and usage should be scheduled.
        /// </summary>
        public abstract IBindable<bool> IsConnected { get; }

        /// <summary>
        /// The states of all users currently being watched.
        /// </summary>
        public IBindableDictionary<int, SpectatorState> WatchingUserStates => watchingUserStates;

        /// <summary>
        /// A global list of all players currently playing.
        /// </summary>
        public IBindableList<int> PlayingUsers => playingUsers;

        /// <summary>
        /// All users currently being watched.
        /// </summary>
        private readonly List<int> watchingUsers = new List<int>();

        private readonly BindableDictionary<int, SpectatorState> watchingUserStates = new BindableDictionary<int, SpectatorState>();
        private readonly BindableList<int> playingUsers = new BindableList<int>();
        private readonly SpectatorState currentState = new SpectatorState();

        private IBeatmap? currentBeatmap;
        private Score? currentScore;

        /// <summary>
        /// Whether the local user is playing.
        /// </summary>
        protected bool IsPlaying { get; private set; }

        /// <summary>
        /// Called whenever new frames arrive from the server.
        /// </summary>
        public event Action<int, FrameDataBundle>? OnNewFrames;

        /// <summary>
        /// Called whenever a user starts a play session, or immediately if the user is being watched and currently in a play session.
        /// </summary>
        public event Action<int, SpectatorState>? OnUserBeganPlaying;

        /// <summary>
        /// Called whenever a user finishes a play session.
        /// </summary>
        public event Action<int, SpectatorState>? OnUserFinishedPlaying;

        [BackgroundDependencyLoader]
        private void load()
        {
            IsConnected.BindValueChanged(connected => Schedule(() =>
            {
                if (connected.NewValue)
                {
                    // get all the users that were previously being watched
                    int[] users = watchingUsers.ToArray();
                    watchingUsers.Clear();

                    // resubscribe to watched users.
                    foreach (int userId in users)
                        WatchUser(userId);

                    // re-send state in case it wasn't received
                    if (IsPlaying)
                        BeginPlayingInternal(currentState);
                }
                else
                {
                    playingUsers.Clear();
                    watchingUserStates.Clear();
                }
            }), true);
        }

        Task ISpectatorClient.UserBeganPlaying(int userId, SpectatorState state)
        {
            Schedule(() =>
            {
                if (!playingUsers.Contains(userId))
                    playingUsers.Add(userId);

                if (watchingUsers.Contains(userId))
                    watchingUserStates[userId] = state;

                OnUserBeganPlaying?.Invoke(userId, state);
            });

            return Task.CompletedTask;
        }

        Task ISpectatorClient.UserFinishedPlaying(int userId, SpectatorState state)
        {
            Schedule(() =>
            {
                playingUsers.Remove(userId);

                if (watchingUsers.Contains(userId))
                    watchingUserStates[userId] = state;

                OnUserFinishedPlaying?.Invoke(userId, state);
            });

            return Task.CompletedTask;
        }

        Task ISpectatorClient.UserSentFrames(int userId, FrameDataBundle data)
        {
            if (data.Frames.Count > 0)
                data.Frames[^1].Header = data.Header;

            Schedule(() => OnNewFrames?.Invoke(userId, data));

            return Task.CompletedTask;
        }

        public void BeginPlaying(GameplayState state, Score score)
        {
            // This schedule is only here to match the one below in `EndPlaying`.
            Schedule(() =>
            {
                if (IsPlaying)
                    throw new InvalidOperationException($"Cannot invoke {nameof(BeginPlaying)} when already playing");

                IsPlaying = true;

                // transfer state at point of beginning play
                currentState.BeatmapID = score.ScoreInfo.BeatmapInfo.OnlineID;
                currentState.RulesetID = score.ScoreInfo.RulesetID;
                currentState.Mods = score.ScoreInfo.Mods.Select(m => new APIMod(m)).ToArray();
                currentState.State = SpectatingUserState.Playing;

                currentBeatmap = state.Beatmap;
                currentScore = score;

                BeginPlayingInternal(currentState);
            });
        }

        public void SendFrames(FrameDataBundle data) => lastSend = SendFramesInternal(data);

        public void EndPlaying(GameplayState state)
        {
            // This method is most commonly called via Dispose(), which is can be asynchronous (via the AsyncDisposalQueue).
            // We probably need to find a better way to handle this...
            Schedule(() =>
            {
                if (!IsPlaying)
                    return;

                if (pendingFrames.Count > 0)
                    purgePendingFrames(true);

                IsPlaying = false;
                currentBeatmap = null;

                if (state.HasPassed)
                    currentState.State = SpectatingUserState.Passed;
                else if (state.HasFailed)
                    currentState.State = SpectatingUserState.Failed;
                else
                    currentState.State = SpectatingUserState.Quit;

                EndPlayingInternal(currentState);
            });
        }

        public void WatchUser(int userId)
        {
            Debug.Assert(ThreadSafety.IsUpdateThread);

            if (watchingUsers.Contains(userId))
                return;

            watchingUsers.Add(userId);

            WatchUserInternal(userId);
        }

        public void StopWatchingUser(int userId)
        {
            // This method is most commonly called via Dispose(), which is asynchronous.
            // Todo: This should not be a thing, but requires framework changes.
            Schedule(() =>
            {
                watchingUsers.Remove(userId);
                watchingUserStates.Remove(userId);
                StopWatchingUserInternal(userId);
            });
        }

        protected abstract Task BeginPlayingInternal(SpectatorState state);

        protected abstract Task SendFramesInternal(FrameDataBundle data);

        protected abstract Task EndPlayingInternal(SpectatorState state);

        protected abstract Task WatchUserInternal(int userId);

        protected abstract Task StopWatchingUserInternal(int userId);

        private readonly Queue<LegacyReplayFrame> pendingFrames = new Queue<LegacyReplayFrame>();

        private double lastSendTime;

        private Task? lastSend;

        private const int max_pending_frames = 30;

        protected override void Update()
        {
            base.Update();

            if (pendingFrames.Count > 0 && Time.Current - lastSendTime > TIME_BETWEEN_SENDS)
                purgePendingFrames();
        }

        public void HandleFrame(ReplayFrame frame)
        {
            Debug.Assert(ThreadSafety.IsUpdateThread);

            if (!IsPlaying)
                return;

            if (frame is IConvertibleReplayFrame convertible)
                pendingFrames.Enqueue(convertible.ToLegacy(currentBeatmap));

            if (pendingFrames.Count > max_pending_frames)
                purgePendingFrames();
        }

        private void purgePendingFrames(bool force = false)
        {
            if (lastSend?.IsCompleted == false && !force)
                return;

            if (pendingFrames.Count == 0)
                return;

            var frames = pendingFrames.ToArray();

            pendingFrames.Clear();

            Debug.Assert(currentScore != null);

            SendFrames(new FrameDataBundle(currentScore.ScoreInfo, frames));

            lastSendTime = Time.Current;
        }
    }
}
