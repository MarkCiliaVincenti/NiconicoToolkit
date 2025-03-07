﻿using CommunityToolkit.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NiconicoToolkit.Video.Watch.Dmc;
using NiconicoToolkit.Video.Watch.NV_Comment;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if WINDOWS_UWP
using Windows.Media.Core;
using Windows.Media.Streaming.Adaptive;
#endif

namespace NiconicoToolkit.Tests
{
    [TestClass]
    public sealed class VideoWatchTest
    {
        [TestInitialize]
        public async Task Initialize()
        {
            _context = new NiconicoContext(AccountTestHelper.Site);
            _context.SetupDefaultRequestHeaders();

            var (status, authority, userId) = await AccountTestHelper.LogInWithTestAccountAsync(_context);

            _userId = userId;
            Guard.IsTrue(status == Account.NiconicoSessionStatus.Success);
        }

        uint _userId;
        NiconicoContext _context;

        #region Domand Hls video

        [TestMethod]
        [DataRow("so42997483")]
        public async Task Domand_PlayVideoAsync(string videoId)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);
            Assert.IsNotNull(res.WatchApiResponse.WatchApiData.Media.Domand);

            var accessRight= await _context.Video.VideoWatch.GetDomandHlsAccessRightAsync(
                videoId
                , res.WatchApiResponse.WatchApiData.Media.Domand
                , res.WatchApiResponse.WatchApiData.Media.Domand.Videos.First(x => x.IsAvailable ?? false)
                , res.WatchApiResponse.WatchApiData.Media.Domand.Audios.First(x => x.IsAvailable ?? false)
                , res.WatchApiResponse.WatchApiData.VideoAds.AdditionalParams.WatchTrackId
                );
            Assert.IsTrue(accessRight.IsSuccess);
            Assert.IsTrue(!string.IsNullOrEmpty(accessRight.Data.ContentUrl));
        }

        [TestMethod]
        [DataRow("so42997483")]
        public async Task Domand_PlayVideoOnlyAudioAsync(string videoId)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);
            Assert.IsNotNull(res.WatchApiResponse.WatchApiData.Media.Domand);

            var accessRight = await _context.Video.VideoWatch.GetDomandHlsAccessRightAsync(
                videoId,
                res.WatchApiResponse.WatchApiData.Media.Domand,
                null,
                res.WatchApiResponse.WatchApiData.Media.Domand.Audios.First(x => x.IsAvailable ?? false).Id,
                res.WatchApiResponse.WatchApiData.VideoAds.AdditionalParams.WatchTrackId
                );
            Assert.IsTrue(accessRight.IsSuccess);
            Assert.IsTrue(!string.IsNullOrEmpty(accessRight.Data.ContentUrl));
        }

        #endregion

        #region Watch Video

#if WINDOWS_UWP

        [TestMethod]
        [DataRow("sm36403134")]
        public async Task PlayVideoProgressiveMp4Async(string videoId)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);
            Assert.IsNotNull(res.WatchApiResponse.WatchApiData.Media.Delivery);

            var movie = res.WatchApiResponse.WatchApiData.Media.Delivery.Movie;
            var session = await _context.Video.VideoWatch.GetDmcSessionResponseAsync(
                res.WatchApiResponse.WatchApiData, movie.Videos.FirstOrDefault(x => x.IsAvailable), movie.Audios.FirstOrDefault(x => x.IsAvailable)
                );

            await OpenProgressiveMp4Async(session);
        }

        [TestMethod]
        [DataRow("sm38647727")]
        public async Task PlayVideoForceHlsAsync(string videoId)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);
            Assert.IsNotNull(res.WatchApiResponse.WatchApiData.Media.Delivery);

            var movie = res.WatchApiResponse.WatchApiData.Media.Delivery.Movie;
            var session = await _context.Video.VideoWatch.GetDmcSessionResponseAsync(
                res.WatchApiResponse.WatchApiData, movie.Videos.FirstOrDefault(x => x.IsAvailable), movie.Audios.FirstOrDefault(x => x.IsAvailable)
                , hlsMode: true
                );

            await OpenHlsAsync(session);
        }

        // TooManyRequestで失敗するためIgnore
        [Ignore]
        [TestMethod]
        [DataRow("so38538458")]
        public async Task PlayVideoHlsAsync(string videoId)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);
            Assert.IsNotNull(res.WatchApiResponse.WatchApiData.Media.Delivery);
            var movie = res.WatchApiResponse.WatchApiData.Media.Delivery.Movie;
            var session = await _context.Video.VideoWatch.GetDmcSessionResponseAsync(
                res.WatchApiResponse.WatchApiData, movie.Videos.FirstOrDefault(x => x.IsAvailable), movie.Audios.FirstOrDefault(x => x.IsAvailable)
                , hlsMode: true
                );

            await OpenHlsAsync(session);
        }

        // TooManyRequestで失敗するためIgnore
        [Ignore]
        [TestMethod]
        [DataRow("so38538458")]
        public async Task PlayVideoForceProgressiveMp4Async(string videoId)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);
            Assert.IsNotNull(res.WatchApiResponse.WatchApiData.Media.Delivery);
            var movie = res.WatchApiResponse.WatchApiData.Media.Delivery.Movie;
            var session = await _context.Video.VideoWatch.GetDmcSessionResponseAsync(
                res.WatchApiResponse.WatchApiData, movie.Videos.FirstOrDefault(x => x.IsAvailable), movie.Audios.FirstOrDefault(x => x.IsAvailable)
                , hlsMode: false
                );

            await OpenProgressiveMp4Async(session);
        }

        private async Task OpenProgressiveMp4Async(DmcSessionResponse session)
        {
            Assert.IsTrue(HttpStatusCodeHelper.IsSuccessStatusCode(session.Meta.Status));
            Debug.WriteLineIf(session.Meta.Message is not null, session.Meta.Message);

            Assert.IsNotNull(session.Data.Session.ContentUri);
            // Try open media
            using (var mediaSource = MediaSource.CreateFromUri(session.Data.Session.ContentUri))
            {
                await mediaSource.OpenAsync();
            }
        }

        private async Task OpenHlsAsync(DmcSessionResponse session)
        {
            Assert.IsTrue(HttpStatusCodeHelper.IsSuccessStatusCode(session.Meta.Status));
            Debug.WriteLineIf(session.Meta.Message is not null, session.Meta.Message);
            Assert.IsNotNull(session.Data.Session.ContentUri);
            Assert.AreEqual("mpeg2ts", session.Data.Session.Protocol.Parameters.HttpParameters.Parameters.HlsParameters.MediaSegmentFormat);

            // Try open media
            var ams = await AdaptiveMediaSource.CreateFromUriAsync(session.Data.Session.ContentUri, _context.HttpClient);
            Assert.AreEqual(ams.Status, AdaptiveMediaSourceCreationStatus.Success);

            using (var mediaSource = MediaSource.CreateFromAdaptiveMediaSource(ams.MediaSource))
            {
                await mediaSource.OpenAsync();
            }
        }

#endif

        [TestMethod]
        [DataRow("so38750114")]
        public async Task GetAdmissionRequireWatchAsync(string videoId)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);

            Assert.IsNull(res.WatchApiResponse.WatchApiData.Media.Delivery);
        }

#endregion



#region Comment

        [TestMethod]
        [DataRow("sm38647727")]
        [DataRow("so41926974")]
        public async Task NvGetCommentAsync(string videoId)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);

            Assert.IsNotNull(res.WatchApiResponse.WatchApiData.Comment);
            
            var commentRes = await _context.Video.NvComment.GetCommentsAsync(res.WatchApiResponse.WatchApiData.Comment.NvComment);

            Assert.IsNotNull(commentRes.Data);
            Assert.IsNotNull(commentRes.Data.Threads);
            Assert.IsNotNull(commentRes.Data.GlobalComments);            
        }

        [TestMethod]
        [DataRow("sm38647727")]
        public async Task NvGetCommentWithoutEasyPostAsync(string videoId)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);

            Assert.IsNotNull(res.WatchApiResponse.WatchApiData.Comment);

            var nvComment = res.WatchApiResponse.WatchApiData.Comment.NvComment;
            var commentRes = await _context.Video.NvComment.GetCommentsAsync(
                nvComment,
                new[] { ThreadTargetForkConstants.Owner, ThreadTargetForkConstants.Main }
                );

            Assert.IsNotNull(commentRes.Data);
            Assert.IsNotNull(commentRes.Data.Threads);            
            Assert.IsNull(commentRes.Data.Threads.FirstOrDefault(x => x.Fork == ThreadTargetForkConstants.Easy));
        }


        [TestMethod]
        [DataRow("sm9", "うぽつ", ThreadTargetForkConstants.Main)]
        public async Task NvPostCommentAsync(string videoId, string commentBody, string threadTarget)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);
           
            Assert.IsNotNull(res.WatchApiResponse.WatchApiData.Comment);

            var comment = res.WatchApiResponse.WatchApiData.Comment;
            var nvComment = comment.NvComment;            
            int vposMs = new Random().Next(res.WatchApiResponse.WatchApiData.Video.Duration * 1000);
            var thread = comment.Threads.FirstOrDefault(x => x.ForkLabel == threadTarget);
            var postKeyRes = await _context.Video.NvComment.GetPostKeyAsync(thread.Id.ToString());
            Assert.IsNotNull(postKeyRes.Data);
            var commentRes = await _context.Video.NvComment.PostCommentAsync(
                nvComment.Server
                , thread.Id.ToString()
                , thread.VideoId
                , new string[] { "184" }
                , commentBody
                , vposMs
                , postKeyRes.Data.PostKey
                );
            
            Assert.IsNotNull(commentRes.Data);
            Assert.IsNotNull(commentRes.Data.Id);            
        }

        [TestMethod]
        [DataRow("sm9", "うぽつ", ThreadTargetForkConstants.Main)]
        public async Task NVPostCommentFailWithInvalidPostKeyAsync(string videoId, string commentBody, string threadTarget)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);

            Assert.IsNotNull(res.WatchApiResponse.WatchApiData.Comment);

            var comment = res.WatchApiResponse.WatchApiData.Comment;
            var nvComment = comment.NvComment;
            int vposMs = new Random().Next(res.WatchApiResponse.WatchApiData.Video.Duration * 1000);
            var thread = comment.Threads.FirstOrDefault(x => x.ForkLabel == threadTarget);
            var postKeyRes = await _context.Video.NvComment.GetPostKeyAsync(thread.Id.ToString());
            Assert.IsNotNull(postKeyRes.Data);

            var commentRes = await _context.Video.NvComment.PostCommentAsync(
                nvComment.Server
                , thread.Id.ToString()
                , thread.VideoId
                , new string[] { "184" }
                , commentBody
                , vposMs
                , ""//postKeyRes.Data.PostKey
                );

            Assert.IsFalse(commentRes.IsSuccess);
            Debug.WriteLine(commentRes.Meta.ErrorCode);
            Debug.WriteLine(commentRes.Meta.Status);
        }


        [TestMethod]
        [DataRow("sm9")]
        public async Task NvEasyPostCommentAsync(string videoId)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);

            Assert.IsNotNull(res.WatchApiResponse.WatchApiData.Comment);

            string commentBody = res.WatchApiResponse.WatchApiData.EasyComment.Phrases.FirstOrDefault()?.Text ?? "うぽつ";

            var comment = res.WatchApiResponse.WatchApiData.Comment;
            var nvComment = comment.NvComment;
            int vposMs = new Random().Next(res.WatchApiResponse.WatchApiData.Video.Duration * 1000);
            var thread = comment.Threads.FirstOrDefault(x => x.ForkLabel == ThreadTargetForkConstants.Easy);
            var easyPostKeyRes = await _context.Video.NvComment.GetEasyPostKeyAsync(thread.Id.ToString());
            Assert.IsNotNull(easyPostKeyRes.Data);
            var commentRes = await _context.Video.NvComment.EasyPostCommentAsync(
                nvComment.Server
                , thread.Id.ToString()
                , thread.VideoId
                , commentBody
                , vposMs
                , easyPostKeyRes.Data.EasyPostKey
                );

            Assert.IsNotNull(commentRes.Data);
            Assert.IsNotNull(commentRes.Data.Id);
        }

        [TestMethod]
        [DataRow("sm9")]
        public async Task NvDeleteCommentAsync(string videoId)
        {
            var res = await _context.Video.VideoWatch.GetInitialWatchDataAsync(videoId, false, false);

            Assert.IsNotNull(res.WatchApiResponse.WatchApiData.Comment, "Not avairable comment in this video : " + videoId);

            var comment = res.WatchApiResponse.WatchApiData.Comment;
            var nvComment = comment.NvComment;
            var commentRes = await _context.Video.NvComment.GetCommentsAsync(nvComment, new[] { ThreadTargetForkConstants.Easy });
            var postedThread = commentRes.Data.Threads.FirstOrDefault(x => x.Fork == ThreadTargetForkConstants.Easy);
            var myPostComments = postedThread.Comments.Where(x => x.IsMyPost);
            int deleteTargetCommentNumber = -1;
            if (myPostComments.Any() is false)
            {
                string commentBody = res.WatchApiResponse.WatchApiData.EasyComment.Phrases.FirstOrDefault()?.Text ?? "うぽつ";
                // コメント投稿する
                int vposMs = new Random().Next(res.WatchApiResponse.WatchApiData.Video.Duration * 1000);
                var postThread = comment.Threads.FirstOrDefault(x => x.ForkLabel == ThreadTargetForkConstants.Easy);
                var easyPostKeyRes = await _context.Video.NvComment.GetEasyPostKeyAsync(postThread.Id.ToString());
                Assert.IsNotNull(easyPostKeyRes.Data, "faield post comment.");
                var postCommentRes = await _context.Video.NvComment.EasyPostCommentAsync(
                    nvComment.Server
                    , postThread.Id.ToString()
                    , postThread.VideoId
                    , commentBody
                    , vposMs
                    , easyPostKeyRes.Data.EasyPostKey
                    );

                deleteTargetCommentNumber = postCommentRes.Data.Number;
                await Task.Delay(1000);
            }
            else
            {
                deleteTargetCommentNumber = myPostComments.First().No;
            }

            var thread = comment.Threads.FirstOrDefault(x => x.ForkLabel == ThreadTargetForkConstants.Easy);
            var keyRes = await _context.Video.NvComment.GetDeleteKeyAsync(thread.Id.ToString(), thread.ForkLabel);
            Assert.IsNotNull(keyRes.Data, "faield getting delete key.");
            var deleteRes = await _context.Video.NvComment.DeleteCommentAsync(
                nvComment.Server
                , thread.VideoId
                , thread.Id.ToString()
                , thread.ForkLabel
                , deleteTargetCommentNumber
                , nvComment.Params.Language
                , keyRes.Data.DeleteKey
                );

            Assert.IsTrue(deleteRes.IsSuccess, "failed comment deletion.");
        }


        #endregion Comment
    }
}
