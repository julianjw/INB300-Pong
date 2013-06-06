using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

using Microsoft.Kinect;
using Coding4Fun.Kinect.WinForm;

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Diagnostics;
using System.Media;

namespace Pong
{
    enum ScalingType { paddleBig, paddleSmall };

    internal static class SkeletalCommonExtensions
    {
        public static Joint ScaleTo(this Joint joint, int width, int height, float skeletonMaxX, float skeletonMaxY, ScalingType scalingType)
        {
            Microsoft.Kinect.SkeletonPoint pos = new SkeletonPoint()
            {
                X = Scale(width, skeletonMaxX, joint.Position.X, scalingType),
                Y = Scale(height, skeletonMaxY, joint.Position.Y, scalingType), //-joint.Position.Y
                Z = joint.Position.Z
            };

            joint.Position = pos;

            return joint;
        }

        public static Joint ScaleTo(this Joint joint, int width, int height)
        {
            return ScaleTo(joint, width, height, 1.0f, 1.0f, ScalingType.paddleBig);
        }

        private static float Scale(int maxPixel, float maxSkeleton, float position, ScalingType scalingType)
        {

            int value = 0;

            if (scalingType == ScalingType.paddleBig) { //smaller area for the arm to swing up and down for the paddle to move

                //Make sure the hand position is within the boundaries of the 0.5 - 0.0 scaled set
                if (position > 0.5f)
                    return 0;
                if (position < 0)
                    return maxPixel;

                //Get the percentage of the hand position within the 0.5 to 0.0 scaled set
                float percent = position / maxSkeleton;

                //Get the new position of the bat using the scaled hand position
                value = (int)(maxPixel - (maxPixel * percent));

            } else if (scalingType == ScalingType.paddleSmall) { //bigger area for the arm to swing up and down for the paddle to move

                //Make sure the hand position is within the boundaries of the 0.75 to -0.5 scaled set
                if (position > 0.75f)
                    return 0;
                if (position < -0.5f)
                    return maxPixel;

                //Change the range so that we can work with the negative values of the hand position
                position += 0.5f;

                //Get the percentage of the hand position within the 2 to 0.5 (1 to -0.5) scaled set
                float percent = position / (maxSkeleton + 0.5f);

                //Get the new position of the bat using the scaled hand position
                value = (int)(maxPixel - (maxPixel * percent));
            }

            //Return the new postion of the paddle scaled
            return value;
        }
    }

    public class CountdownTimer
    {
        int count;
        Stopwatch clock;

        public CountdownTimer(int time)
        {
            count = time;
            clock = new Stopwatch();
        }

        public int GetSeconds()
        {
            TimeSpan now = clock.Elapsed;
            int secondsPassed = now.Seconds;
            int result = count - secondsPassed;
            if (result < 0) result = 0;
            return result;
        }

        public void Start()
        {
            clock.Start();
        }

        public void Stop()
        {
            clock.Stop();
        }
    }

	public class PongGame : Microsoft.Xna.Framework.Game
	{
        const string gameTitle = "Pong 9001";
        const int kLRMargin = 20, kPaddleWidth = 26, kPaddleHeight = 200, kSmallPaddleHeight = 120, kBigPaddleHeight = 300;
		const int kBallWidth = 24, kBallHeight = 24;
		const int kMaxAIPaddleVelocity = 7;
		const int kGameWidth = 1920, kGameHeight = 1080;
        const int hMidPoint = kGameWidth / 2;
        const int vMidPoint = kGameHeight / 2;

        int[] playerPaddleHeights = { 0, kBigPaddleHeight, kPaddleHeight, kSmallPaddleHeight, kBigPaddleHeight, kPaddleHeight, 0 };
        int[] aiPaddleHeights = { 0, kSmallPaddleHeight, kPaddleHeight, kSmallPaddleHeight, kPaddleHeight, kPaddleHeight, 0 };
		
		bool passedCenter = false;
		
		GraphicsDeviceManager graphics;
		SpriteBatch spriteBatch;
		
		Texture2D dotTexture = null, ballTexture = null;
		
		Rectangle player1PaddleRectRight = new Rectangle(kLRMargin + 60, 0, kPaddleWidth, kPaddleHeight);
        Rectangle player1PaddleRectLeft = new Rectangle(kLRMargin, 0, kPaddleWidth, kPaddleHeight);

        Rectangle player2PaddleRectRight = new Rectangle(kGameWidth - kLRMargin - kPaddleWidth, 20, kPaddleWidth, kPaddleHeight);
        Rectangle player2PaddleRectLeft = new Rectangle(kGameWidth - kLRMargin - kPaddleWidth - 60, 20, kPaddleWidth, kPaddleHeight);

		Rectangle aiPaddleRectRed;
        Rectangle aiPaddleRectBlue;
		
		Vector2 ballRedVelocity;
        Vector2 ballBlueVelocity;

        Vector2 zeroVelocity = new Vector2(0.0f, 0.0f);

		Rectangle ballRedRect;
        Rectangle ballBlueRect;
		
		float predictedBallRedHeight = 0.0f;
        float predictedBallBlueHeight = 0.0f;

        int player1Score = 0;
        int player2Score = 0;

        int player1GameScore = 0;
        int player2GameScore = 0;

        SpriteFont gameFont;
        SpriteFont titleFont;
        SpriteFont instructionsFont;

        float handPos;

        DateTime time = new DateTime();

        int gameLevel = 0;
        enum state { START, TRANS, PLAY, END, SCORE };
        state gameState = state.START;

        int currentGameLevel = 0;
        int gameMode = 0;

        CountdownTimer timer = new CountdownTimer(5);

        enum playerMode { singlePlayer, multiPlayer };

        SoundEffect bounceSound;
        SoundEffect backgroundSound;
        SoundEffectInstance backgroundSoundInstance;
        SoundEffect gameoverSound;
        SoundEffectInstance gameoverSoundInstance;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

		public PongGame()
		{
			graphics = new GraphicsDeviceManager(this);
			graphics.PreferredBackBufferWidth = kGameWidth;
			graphics.PreferredBackBufferHeight = kGameHeight;
            
            //graphics.PreferMultiSampling = false;
            graphics.IsFullScreen = true;
			
			Content.RootDirectory = "Content";
		}
		
		private void RestartGame()
		{
            //if (gameLevel == 0)
            if (gameState == state.START)
            {
                if (backgroundSound == null)
                {
                    backgroundSound = Content.Load<SoundEffect>("BackgroundMusic1");
                    backgroundSoundInstance = backgroundSound.CreateInstance();
                    backgroundSoundInstance.Volume = 0.5f;
                    backgroundSoundInstance.IsLooped = true;
                    backgroundSoundInstance.Play();
                }
                else if (backgroundSoundInstance.State == SoundState.Paused)
                {
                    backgroundSoundInstance.Resume();
                }
            }

            setPaddles();

            ballRedRect = new Rectangle(500, 600, kBallWidth, kBallHeight);
            ballBlueRect = new Rectangle(500, 300, kBallWidth, kBallHeight);

            if (player1Score >= 3)
            {
                //set text to player 1 wins
                //gameText = "Player 1 Wins!";
                ballRedVelocity = zeroVelocity;
                ballBlueVelocity = zeroVelocity;
                //Progress the game and tally "Game" score
                currentGameLevel = gameLevel;
                if (gameLevel == 5)
                {
                    //gameLevel = 6;
                    gameState = state.END;
                    //currentGameLevel = 6;
                    backgroundSoundInstance.Pause();
                    gameoverSoundInstance.Play();
                    while (gameoverSoundInstance.State == SoundState.Playing)
                    {
                        continue;
                    }
                    backgroundSoundInstance.Resume();
                }
                else
                {
                    //gameLevel = 7;
                    gameState = state.TRANS;
                    gameLevel++;
                    setPaddles();
                    timer = new CountdownTimer(5);
                }
                player1GameScore++;
            }
            else if (player2Score >= 3)
            {
                //set text to player 2 wins
                //gameText = "AI Wins!";
                ballRedVelocity = zeroVelocity;
                ballBlueVelocity = zeroVelocity;
                //Progress the game and tally "Game" score
                currentGameLevel = gameLevel;
                if (gameLevel == 5)
                {
                    //gameLevel = 6;
                    gameState = state.END;
                    currentGameLevel = 6;
                    backgroundSoundInstance.Pause();
                    gameoverSoundInstance.Play();
                    while (gameoverSoundInstance.State == SoundState.Playing)
                    {
                        continue;
                    }
                    backgroundSoundInstance.Resume();
                }
                else
                {
                    //gameLevel = 7;
                    gameState = state.TRANS;
                    gameLevel++;
                    setPaddles();
                    timer = new CountdownTimer(5);
                }
                player2GameScore++;
            }
            else
            {
                ballRedVelocity = zeroVelocity;
                ballBlueVelocity = zeroVelocity;

                ballRedVelocity = RandomVelocity();
            }
		}

        private void setPaddles()
        {
            // first paddle, left hand side for player
            player1PaddleRectLeft = new Rectangle(kLRMargin, 0, kPaddleWidth, playerPaddleHeights[gameLevel]);
            // right hand side for AI
            aiPaddleRectRed = new Rectangle(GraphicsDevice.Viewport.Width - kLRMargin - kPaddleWidth, 20, kPaddleWidth, aiPaddleHeights[gameLevel]);

            if (gameLevel > 3) // dual paddles
            {
                // second paddle, right hand side for player
                player1PaddleRectRight = new Rectangle(kLRMargin + 60, 0, kPaddleWidth, playerPaddleHeights[gameLevel]);
                // left hand side for AI
                aiPaddleRectBlue = new Rectangle(GraphicsDevice.Viewport.Width - kLRMargin - kPaddleWidth - 60, 20, kPaddleWidth, aiPaddleHeights[gameLevel]);
            }
        }

        private Vector2 RandomVelocity()
        {
            Vector2 randomVelocity = new Vector2();

            //change velocity to random direction towards AI
            randomVelocity = new Vector2((float)new Random(time.Millisecond).Next(5, 8), (float)new Random(time.Millisecond).Next(-8, 8));

            while (randomVelocity.Y == 0 || (randomVelocity.Y >= -5 && randomVelocity.Y <= 0) || (randomVelocity.Y <= 5 && randomVelocity.Y >= 0))
            {
                randomVelocity.Y = new Random().Next(-8, 8);
            }

            return randomVelocity;
        }
		
		protected override void Initialize()
		{
			RestartGame();

            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }
			
			base.Initialize();
		}

        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            if (skeletons.Length != 0)
                {
                foreach (Skeleton skel in skeletons) 
                {
                    if (SkeletonTrackingState.Tracked == skel.TrackingState)
                    {
                        Joint jointRight = skel.Joints[JointType.HandRight];
                        Joint jointLeft = skel.Joints[JointType.HandLeft];

                        int handRightY = 0;
                        int handLeftY = 0;

                        handPos = jointLeft.Position.Y; //for testing purposes, can be deleted for release version   

                        switch (gameLevel)
                        {
                            case 0: 
                                Joint jointHead = skel.Joints[JointType.Head];
                                if (jointLeft.Position.Y >= jointHead.Position.Y)
                                {
                                    gameLevel = 1;
                                    gameState = state.TRANS;
                                    RestartGame();
                                }
                                break;
                            //gamelevel 1 = one extremely big
                            case 1:
                                handLeftY = (int)SkeletalCommonExtensions.ScaleTo(jointLeft, kGameWidth, kGameHeight - (kPaddleHeight*2), 0.5f, 0.5f, ScalingType.paddleBig).Position.Y;
                                break;
                            //gamelevel 2 = one big
                            case 2:
                                handLeftY = (int)SkeletalCommonExtensions.ScaleTo(jointLeft, kGameWidth, kGameHeight - kPaddleHeight, 0.5f, 0.5f, ScalingType.paddleBig).Position.Y;
                                break;
                            //gamelevel 3 = one small
                            case 3:
                                handLeftY = (int)SkeletalCommonExtensions.ScaleTo(jointLeft, kGameWidth, kGameHeight - kSmallPaddleHeight, 0.75f, 0.75f, ScalingType.paddleSmall).Position.Y;
                                break;
                            //gamelevel 4 = two big
                            case 4:
                                handRightY = (int)SkeletalCommonExtensions.ScaleTo(jointRight, kGameWidth, kGameHeight - kPaddleHeight, 0.5f, 0.5f, ScalingType.paddleBig).Position.Y;
                                handLeftY = (int)SkeletalCommonExtensions.ScaleTo(jointLeft, kGameWidth, kGameHeight - kPaddleHeight, 0.5f, 0.5f, ScalingType.paddleBig).Position.Y;
                                break;
                            //gamelvel 5 = one big, one small
                            case 5:
                                handLeftY = (int)SkeletalCommonExtensions.ScaleTo(jointLeft, kGameWidth, kGameHeight - kPaddleHeight, 0.5f, 0.5f, ScalingType.paddleBig).Position.Y;
                                handRightY = (int)SkeletalCommonExtensions.ScaleTo(jointRight, kGameWidth, kGameHeight - kSmallPaddleHeight, 0.75f, 0.75f, ScalingType.paddleSmall).Position.Y;
                                break;
                            //default: break;
                        }

                        //if multiplayer blah blah blah
                        if (gameMode == (int)playerMode.singlePlayer)
                        {
                            //if (skel == skeletons[0])
                            //{
                                player1PaddleRectRight.Y = handRightY;
                                player1PaddleRectLeft.Y = handLeftY;
                            //}
                        }
                        //else if (gameMode == (int)playerMode.multiPlayer)
                        //{
                        //    if (skel == skeletons[1])
                        //    {
                        //        player2PaddleRectRight.Y = handRightY;
                        //        player2PaddleRectLeft.Y = handLeftY;
                        //    }
                        //}

                        break;
                    }
                }
            }
        }

		protected override void LoadContent()
		{
			spriteBatch = new SpriteBatch(GraphicsDevice);

            gameFont = Content.Load<SpriteFont>("Scoreboard");
            titleFont = Content.Load<SpriteFont>("Title");
            instructionsFont = Content.Load<SpriteFont>("Instructions");

			dotTexture = Content.Load<Texture2D>("Dot");
			ballTexture = Content.Load<Texture2D>("Ball");

            bounceSound = Content.Load<SoundEffect>("bounce");
            gameoverSound = Content.Load<SoundEffect>("gameover");
            gameoverSoundInstance = gameoverSound.CreateInstance();
            gameoverSoundInstance.IsLooped = false;
		}
		
		protected override void UnloadContent()
		{
            if (this.sensor != null)
            {
                this.sensor.Stop();
            }
		}
		
		private void SimulateRestOfTurn()
		{
			Rectangle currentBallRectRed = ballRedRect;
            Rectangle currentBallRectBlue = ballBlueRect;

			Vector2 currentballRedVelocity = ballRedVelocity;
            Vector2 currentballBlueVelocity = ballBlueVelocity;
			
			bool done = false;
			
			while (!done)
			{
				BallCollision resultRed = AdjustBallRedPositionWithScreenBounds(ref currentBallRectRed, ref currentballRedVelocity);
                BallCollision resultBlue = AdjustBallBluePositionWithScreenBounds(ref currentBallRectBlue, ref currentballBlueVelocity);
				done = (resultRed == BallCollision.RightMiss || resultBlue == BallCollision.RightMiss || resultBlue == BallCollision.RightPaddle || resultRed == BallCollision.RightPaddle || resultRed == BallCollision.LeftPaddle ||
                        resultBlue == BallCollision.LeftPaddle);
			}

            predictedBallBlueHeight = currentBallRectBlue.Y + new Random(time.Millisecond).Next(-150, 150);

            if (gameLevel != 1 && gameLevel != 3 && gameLevel != 5)
            {
                predictedBallRedHeight = currentBallRectRed.Y + new Random(time.Millisecond).Next(-150, 150);
            }
            else
            {
                predictedBallRedHeight = currentBallRectRed.Y + new Random(time.Millisecond).Next(-75, 75);
            }
		}
		
		enum BallCollision
		{
			None,
			RightPaddle,
			LeftPaddle,
			RightMiss,
			LeftMiss
		}
		
		private BallCollision AdjustBallRedPositionWithScreenBounds(ref Rectangle enclosingRectRed, ref Vector2 velocityRed)
		{
			BallCollision collision = BallCollision.None;
			
            //Red ball collision detection
			enclosingRectRed.X += (int)velocityRed.X;
			enclosingRectRed.Y += (int)velocityRed.Y;
			
			if (enclosingRectRed.Y >= GraphicsDevice.Viewport.Height - kBallHeight)
			{
				velocityRed.Y *= -1;
                bounceSound.Play();
			}
			else if (enclosingRectRed.Y <= 0)
			{
				velocityRed.Y *= -1;
                bounceSound.Play();
			}
			
			if (aiPaddleRectRed.Intersects(enclosingRectRed))
			{
                float oldVelocityX = velocityRed.X;
                //Make sure the ball doesn't get stuck within the player's paddle
                if (enclosingRectRed.Right > aiPaddleRectRed.X && (enclosingRectRed.Bottom > aiPaddleRectRed.Y && enclosingRectRed.Y < aiPaddleRectRed.Bottom))
                {

                    if (enclosingRectRed.Bottom >= aiPaddleRectRed.Top && enclosingRectRed.Right > aiPaddleRectRed.Center.X)
                    {

                        velocityRed.Y *= -1;
                        enclosingRectRed.Y -= 2;
                    }
                    else if (enclosingRectRed.Top <= aiPaddleRectRed.Bottom && enclosingRectRed.Right > aiPaddleRectRed.Center.X)
                    {
                        velocityRed.Y *= -1;
                        enclosingRectRed.Y += 2;
                    }
                    else
                    {
                        velocityRed.X *= -1;
                        enclosingRectRed.X = aiPaddleRectRed.X - enclosingRectRed.Width - 5;
                    }

                    if (velocityRed.X == 0)
                    {
                        velocityRed.X = -0.5f;
                    }
                }
                else
                {
                    velocityRed.X *= -1;
                }
                bounceSound.Play();
                collision = BallCollision.RightPaddle;
			}
			else if (player1PaddleRectLeft.Intersects(enclosingRectRed))
			{
                float oldVelocityX = velocityRed.X;
                //Make sure the ball doesn't get stuck within the player's paddle
                //if ((enclosingRectRed.X) < (player1PaddleRectLeft.X + player1PaddleRectLeft.Width))
                if (enclosingRectRed.Left < player1PaddleRectLeft.Right && (enclosingRectRed.Bottom > player1PaddleRectLeft.Y && enclosingRectRed.Y < player1PaddleRectLeft.Bottom))
                {

                    if (enclosingRectRed.Bottom >= player1PaddleRectLeft.Top && enclosingRectRed.Left > player1PaddleRectLeft.Center.X)
                    {

                        velocityRed.Y *= -1;
                        enclosingRectRed.Y -= 2;
                    }
                    else if (enclosingRectRed.Top <= player1PaddleRectLeft.Bottom && enclosingRectRed.Left > player1PaddleRectLeft.Center.X)
                    {
                        velocityRed.Y *= -1;
                        enclosingRectRed.Y += 2;
                    }
                    else
                    {
                        velocityRed.X *= -1;
                        enclosingRectRed.X = player1PaddleRectLeft.Right + 5;
                    }

                    if (velocityRed.X == 0)
                    {
                        velocityRed.X = 0.5f;
                    }
                }
                else
                {
                    velocityRed.X *= -1;
                }
                bounceSound.Play();
				collision = BallCollision.LeftPaddle;
			}
			else if (enclosingRectRed.X >= GraphicsDevice.Viewport.Width - kBallWidth)
			{
				collision = BallCollision.RightMiss;
			}
			else if (enclosingRectRed.X <= 0)
			{
				collision = BallCollision.LeftMiss;
			}
			
			return collision;
		}

        private BallCollision AdjustBallBluePositionWithScreenBounds(ref Rectangle enclosingRectBlue, ref Vector2 velocityBlue)
        {
            BallCollision collision = BallCollision.None;

            //Blue ball collision detection
            enclosingRectBlue.X += (int)velocityBlue.X;
            enclosingRectBlue.Y += (int)velocityBlue.Y;

            if (enclosingRectBlue.Y >= GraphicsDevice.Viewport.Height - kBallHeight)
            {
                velocityBlue.Y *= -1;
                bounceSound.Play();
            }
            else if (enclosingRectBlue.Y <= 0)
            {
                velocityBlue.Y *= -1;
                bounceSound.Play();
            }

            if (aiPaddleRectBlue.Intersects(enclosingRectBlue))
            {
                float oldVelocityX = velocityBlue.X;
                //Make sure the ball doesn't get stuck within the player's paddle

                if (enclosingRectBlue.Right > aiPaddleRectBlue.X && (enclosingRectBlue.Bottom > aiPaddleRectBlue.Y && enclosingRectBlue.Y < aiPaddleRectBlue.Bottom))
                {

                    if (enclosingRectBlue.Bottom >= aiPaddleRectBlue.Top && enclosingRectBlue.Right > aiPaddleRectBlue.Center.X)
                    {

                        velocityBlue.Y *= -1;
                        enclosingRectBlue.Y -= 2;
                    }
                    else if (enclosingRectBlue.Top <= aiPaddleRectBlue.Bottom && enclosingRectBlue.Right > aiPaddleRectBlue.Center.X)
                    {
                        velocityBlue.Y *= -1;
                        enclosingRectBlue.Y += 2;
                    }
                    else
                    {
                        velocityBlue.X *= -1;
                        enclosingRectBlue.X = aiPaddleRectBlue.X - enclosingRectBlue.Width - 5;
                    }

                    if (velocityBlue.X == 0)
                    {
                        velocityBlue.X = -0.5f;
                    }

                }
                else
                {
                    velocityBlue.X *= -1;
                }
                bounceSound.Play();
                collision = BallCollision.RightPaddle;
            }
            else if (player1PaddleRectRight.Intersects(enclosingRectBlue))
            {
                float oldVelocityX = velocityBlue.X;
                //Make sure the ball doesn't get stuck within the player's paddle
                //if ((enclosingRectBlue.X) < (player1PaddleRectRight.X + player1PaddleRectRight.Width))
                if (enclosingRectBlue.Left < player1PaddleRectRight.Right && (enclosingRectBlue.Bottom > player1PaddleRectRight.Y && enclosingRectBlue.Y < player1PaddleRectRight.Bottom))
                {

                    if (enclosingRectBlue.Bottom >= player1PaddleRectRight.Top && enclosingRectBlue.Left > player1PaddleRectRight.Center.X)
                    {

                        velocityBlue.Y *= -1;
                        enclosingRectBlue.Y -= 2;
                    }
                    else if (enclosingRectBlue.Top <= player1PaddleRectRight.Bottom && enclosingRectBlue.Left > player1PaddleRectRight.Center.X)
                    {
                        velocityBlue.Y *= -1;
                        enclosingRectBlue.Y += 2;
                    }
                    else
                    {
                        velocityBlue.X *= -1;
                        enclosingRectBlue.X = player1PaddleRectRight.Right + 5;
                    }

                    if (velocityBlue.X == 0)
                    {
                        velocityBlue.X = -0.5f;
                    }
                }
                else
                {
                    velocityBlue.X *= -1;
                }
                bounceSound.Play();
                collision = BallCollision.LeftPaddle;
            }
            else if (enclosingRectBlue.X >= GraphicsDevice.Viewport.Width - kBallWidth)
            {
                collision = BallCollision.RightMiss;
            }
            else if (enclosingRectBlue.X <= 0)
            {
                collision = BallCollision.LeftMiss;
            }

            return collision;
        }

        protected override void Update(GameTime gameTime)
        {
            keyboardInputs();

            //if (gameLevel == 7)
            if (gameState == state.TRANS)
            {
                timer.Start();

                if (timer.GetSeconds() == 0)
                {
                    //gameLevel = currentGameLevel + 1;
                    //gameLevel++;
                    //currentGameLevel = 7;
                    gameState = state.PLAY;
                    resetScore();
                    timer.Stop();
                }
            }
            //else if (gameLevel > 0 && gameLevel <= 5)
            else if (gameState == state.PLAY)
            {
                // TODO remove this IF statement I think
                //if (currentGameLevel == 7)
                //{
                //    RestartGame();
                //currentGameLevel = 0;
                //}

                BallCollision collisionRed = AdjustBallRedPositionWithScreenBounds(ref ballRedRect, ref ballRedVelocity);
                BallCollision collisionBlue = AdjustBallBluePositionWithScreenBounds(ref ballBlueRect, ref ballBlueVelocity);

                if (collisionRed > 0)
                {
                    passedCenter = false;

                    float newY = (new Random().Next(80) + 1) / 10.0f;
                    ballRedVelocity.Y = ballRedVelocity.Y > 0 ? newY : -newY;
                }

                if (collisionBlue > 0)
                {
                    passedCenter = false;

                    float newY = (new Random().Next(80) + 1) / 10.0f;
                    ballBlueVelocity.Y = ballBlueVelocity.Y > 0 ? newY : -newY;
                }

                if (collisionRed == BallCollision.RightMiss || collisionRed == BallCollision.LeftMiss)
                {
                    //Changes the score to reflect who won the point (who missed the ball)
                    if (BallCollision.RightMiss == collisionRed)
                    {
                        player1Score += 1;
                    }
                    else if (BallCollision.LeftMiss == collisionRed)
                    {
                        player2Score += 1;
                    }

                    RestartGame();
                } else if (collisionBlue == BallCollision.RightMiss || collisionBlue == BallCollision.LeftMiss)
                {
                    //Changes the score to reflect who won the point (who missed the ball)
                    if (BallCollision.RightMiss == collisionBlue)
                    {
                        player1Score += 1;
                    }
                    else if (BallCollision.LeftMiss == collisionBlue)
                    {
                        player2Score += 1;
                    }

                    RestartGame();
                }

                //send blue ball out once red ball has passed the center after hitting the AI's paddle
                if (gameState == state.PLAY && (gameLevel == 4 || gameLevel == 5)) {

                    if (passedCenter == false && ballRedVelocity.X < 0 && (ballRedRect.X + kBallWidth <= GraphicsDevice.Viewport.Bounds.Center.X))
                    {
                        if (ballBlueVelocity == zeroVelocity)
                        {
                            ballBlueVelocity = RandomVelocity();
                        }
                    }
                }

                if (passedCenter == false && ballRedVelocity.X > 0 && (ballRedRect.X + kBallWidth >= GraphicsDevice.Viewport.Bounds.Center.X))
                {
                    SimulateRestOfTurn();
                    passedCenter = true;
                }

                if (passedCenter == false && ballBlueVelocity.X > 0 && (ballBlueRect.X + kBallWidth >= GraphicsDevice.Viewport.Bounds.Center.X))
                {
                    SimulateRestOfTurn();
                    passedCenter = true; //breakpoint please
                }

                int ballCenterRed = (int)predictedBallRedHeight + (kBallHeight / 2);
                int ballCenterBlue = (int)predictedBallBlueHeight + (kBallHeight / 2);

                //AI Red Paddle
                int aiPaddleCenterRed = aiPaddleRectRed.Center.Y;

                if (predictedBallRedHeight > 0 && ballCenterRed != aiPaddleCenterRed)
                {

                    if (((((aiPaddleRectRed.Y + kSmallPaddleHeight) <= kGameHeight) && (gameLevel == 1 || gameLevel == 3 || gameLevel == 5))) || 
                        (((aiPaddleRectRed.Y + kPaddleHeight) <= kGameHeight) && (gameLevel == 2 || gameLevel == 4)))
                    {
                        if (ballCenterRed < aiPaddleCenterRed)
                        {
                            aiPaddleRectRed.Y -= kMaxAIPaddleVelocity;
                        }
                        else if (ballCenterRed > aiPaddleCenterRed)
                        {
                            aiPaddleRectRed.Y += kMaxAIPaddleVelocity;
                        }

                        if (Math.Abs(ballCenterRed - aiPaddleCenterRed) < kMaxAIPaddleVelocity)
                        {
                            if (gameLevel == 1 || gameLevel == 3)
                            {
                                aiPaddleRectRed.Y = ballCenterRed - (kSmallPaddleHeight / 2);
                            }
                            else if (gameLevel == 2 || gameLevel == 4)
                            {
                                aiPaddleRectRed.Y = ballCenterRed - (kPaddleHeight / 2);
                            }
                            else
                            {
                                aiPaddleRectRed.Y = ballCenterRed - (kPaddleHeight / 2);
                            }
                        }
                    }
                    else
                    {
                        if (gameLevel == 1 || gameLevel == 3 || gameLevel == 5)
                        {
                            aiPaddleRectRed.Y = kGameHeight - kSmallPaddleHeight;
                        }
                        else
                        {
                            aiPaddleRectRed.Y = kGameHeight - kPaddleHeight;
                        }
                    }
                }

                //AI Blue Paddle
                int aiPaddleCenterBlue = aiPaddleRectBlue.Center.Y;

                if (predictedBallBlueHeight > 0 && ballCenterBlue != aiPaddleCenterBlue)
                {
                    if ((aiPaddleRectBlue.Y + kPaddleHeight) > kGameHeight)
                    {
                        aiPaddleRectBlue.Y = kGameHeight - kPaddleHeight;
                    }
                    else
                    {
                        if (ballCenterBlue < aiPaddleCenterBlue)
                        {
                            aiPaddleRectBlue.Y -= kMaxAIPaddleVelocity;
                        }
                        else if (ballCenterBlue > aiPaddleCenterBlue)
                        {
                            aiPaddleRectBlue.Y += kMaxAIPaddleVelocity;
                        }

                        if (Math.Abs(ballCenterBlue - aiPaddleCenterBlue) < kMaxAIPaddleVelocity)
                        {
                            if (gameLevel == 5)
                            {
                                aiPaddleRectBlue.Y = ballCenterBlue - (kSmallPaddleHeight / 2);
                            }
                            else
                            {
                                aiPaddleRectBlue.Y = ballCenterBlue - (kPaddleHeight / 2);
                            }
                        }
                    }
                }
            }

            base.Update(gameTime);
        }

        private void keyboardInputs()
        {
            if (Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.Escape) || Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.Q))
                this.Exit();

            if (Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.N))
            {
                gameLevel = 0;
                gameState = state.START;
                gameMode = (int)playerMode.singlePlayer;
                resetGameScore();
            }

            if (Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.NumPad1) || Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.D1))
            {
                gameLevel = 1;
                gameState = state.PLAY;
                resetScore();
            }

            if (Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.NumPad2) || Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.D2))
            {
                gameLevel = 2;
                gameState = state.PLAY;
                resetScore();
            }

            if (Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.NumPad3) || Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.D3))
            {
                gameLevel = 3;
                gameState = state.PLAY;
                resetScore();
            }

            if (Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.NumPad4) || Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.D4))
            {
                gameLevel = 4;
                gameState = state.PLAY;
                resetScore();
            }

            if (Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.NumPad5) || Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.D5))
            {
                gameLevel = 5;
                gameState = state.PLAY;
                resetScore();
            }

            if (Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.NumPad6) || Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.D6))
            {
                gameLevel = 6;
                gameState = state.END;
                resetScore();
            }
        }

        private void resetScore()
        {
            player1Score = 0;
            player2Score = 0;
            RestartGame();
        }

        private void resetGameScore()
        {
            player1GameScore = 0;
            player2GameScore = 0;
            resetScore();
        }
		
		protected override void Draw(GameTime gameTime)
		{
            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin();

            switch (gameState)
            {
                case state.START:
                    drawTitleScreen();
                    break;
                case state.END:
                    drawEndScreen();
                    break;
                case state.TRANS:
                    drawTransitionScreen();
                    drawPaddles();
                    break;
                case state.PLAY:
                    drawBalls();
                    drawPaddles();
                    drawScore();
                    break;
            }

			spriteBatch.End();
			
			base.Draw(gameTime);
		}

        private void drawBalls()
        {
            if (gameMode == (int)playerMode.singlePlayer)
            {
                //Draw the 1st ball
                spriteBatch.Draw(ballTexture, ballRedRect, Color.Red);

                if (gameLevel == 4 || gameLevel == 5)
                {
                    //Draw the 2nd ball
                    spriteBatch.Draw(ballTexture, ballBlueRect, Color.Blue);
                }
            }
            else if (gameMode == (int)playerMode.multiPlayer) //multiplayer skeleton code
            {

                switch (gameLevel)
                {
                    case 1:

                        break;
                    case 2:

                        break;
                    case 3:

                        break;
                    case 4:

                        break;
                    default: break;
                }
            }
        }

        private void drawPaddles()
        {
            if (gameMode == (int)playerMode.singlePlayer)
            {
                // Draw the player's 1st paddle
                spriteBatch.Draw(dotTexture, player1PaddleRectLeft, Color.Red);

                // Draw the AI's 1st paddle
                spriteBatch.Draw(dotTexture, aiPaddleRectRed, Color.OrangeRed);

                if (gameLevel == 4 || gameLevel == 5)
                {
                    // Draw the player's 2nd paddle
                    spriteBatch.Draw(dotTexture, player1PaddleRectRight, Color.Blue);

                    // Draw the AI's 2nd paddle
                    spriteBatch.Draw(dotTexture, aiPaddleRectBlue, Color.Purple);
                }
            }
            else if (gameMode == (int)playerMode.multiPlayer) //multiplayer skeleton code
            {

                switch (gameLevel)
                {
                    case 1:
                    case 2:

                        break;
                    case 3:

                        break;
                    case 4:

                        break;
                    default: break;
                }

            }
        }

        private void drawTitleScreen()
        {
            string tagline = "Yo dawg I heard you like PONG!";
            string instructions = "Raise your left hand to start game";

            // Text sizes according to font package
            Vector2 titleTxtSize = titleFont.MeasureString(gameTitle);
            Vector2 tagTxtSize = gameFont.MeasureString(tagline);
            Vector2 insTxtSize = instructionsFont.MeasureString(instructions);

            // Text positions according to sizes
            Vector2 titleTxtPos = new Vector2(hMidPoint - (titleTxtSize.X / 2), vMidPoint - titleTxtSize.Y);
            Vector2 tagTxtPos = new Vector2(hMidPoint - (tagTxtSize.X / 2), vMidPoint);
            Vector2 insTxtPos = new Vector2(hMidPoint - (insTxtSize.X / 2), kGameHeight - insTxtSize.Y);

            // Draw text
            spriteBatch.DrawString(titleFont, (gameTitle), titleTxtPos, Color.White);
            spriteBatch.DrawString(gameFont, (tagline), tagTxtPos, Color.White);
            spriteBatch.DrawString(instructionsFont, (instructions), insTxtPos, Color.White);
        }

        private void drawEndScreen()
        {
            string player2, winner, endMessage;

            // Title text size according to font package
            Vector2 titleTxtSize = titleFont.MeasureString(gameTitle);

            // Title text position according to size and draw title
            Vector2 titleTxtPos = new Vector2(hMidPoint - (titleTxtSize.X / 2), vMidPoint - titleTxtSize.Y);
            spriteBatch.DrawString(titleFont, (gameTitle), titleTxtPos, Color.White);

            // determine who player 2 is
            player2 = (gameMode == (int)playerMode.singlePlayer) ? "Computer" : "Player 2";
            // determine the winner
            winner = (player1GameScore > player2GameScore) ? "Player 1" : player2;
            // determine if there's a tie or a winner
            endMessage = (player1GameScore == player2GameScore) ? endMessage = "Tie!" : endMessage = winner + " Wins!";

            // Win message size according to font package
            Vector2 tagTxtSize = gameFont.MeasureString(endMessage);

            // Win message position according to size and draw
            Vector2 tagTxtPos = new Vector2(hMidPoint - (tagTxtSize.X / 2), vMidPoint);
            spriteBatch.DrawString(gameFont, (endMessage), tagTxtPos, Color.White);

        }

        private void drawTransitionScreen()
        {

            string level = "Level: " + gameLevel;
            string message = "Get READY!";
            string countdown = timer.GetSeconds().ToString();
            string score = player1GameScore + "           SCORE           " + player2GameScore;

            // Text sizes according to font package
            Vector2 lvlTxtSize = titleFont.MeasureString(level);
            Vector2 msgTxtSize = gameFont.MeasureString(message);
            Vector2 timeTxtSize = gameFont.MeasureString(countdown);
            Vector2 scrTxtSize = gameFont.MeasureString(score);

            // Text positions according to sizes
            Vector2 lvlTxtPos = new Vector2(hMidPoint - (lvlTxtSize.X / 2), vMidPoint - lvlTxtSize.Y);
            Vector2 msgTxtPos = new Vector2(hMidPoint - (msgTxtSize.X / 2), vMidPoint);
            Vector2 timeTxtPos = new Vector2(hMidPoint - (timeTxtSize.X / 2), vMidPoint + msgTxtSize.Y);
            Vector2 scrTxtPos = new Vector2(hMidPoint - (scrTxtSize.X / 2), kGameHeight - (scrTxtSize.Y * 2));

            // Draw text
            spriteBatch.DrawString(titleFont, (level), lvlTxtPos, Color.White);
            spriteBatch.DrawString(gameFont, (message), msgTxtPos, Color.White);
            spriteBatch.DrawString(gameFont, (countdown), timeTxtPos, Color.White);
            spriteBatch.DrawString(gameFont, (score), scrTxtPos, Color.White);

            // TODO remove debug lines below:
            if (currentGameLevel + 1 == 7)
            {
                level = "WRONG";
            }
        }

        private void drawScore()
        {
            string level = "Level: " + gameLevel;
            string score = player1Score + " | " + player2Score;

            // Text sizes according to font package
            Vector2 lvlTxtSize = gameFont.MeasureString(level);
            Vector2 scrTxtSize = gameFont.MeasureString(score);

            // Text positions according to sizes
            Vector2 levelTxtPos = new Vector2(hMidPoint - (lvlTxtSize.X / 2), 0);
            Vector2 scoreTxtPos = new Vector2(hMidPoint - (scrTxtSize.X / 2), (lvlTxtSize.Y));

            // Draw text
            spriteBatch.DrawString(gameFont, (level), levelTxtPos, Color.White);
            spriteBatch.DrawString(gameFont, (score), scoreTxtPos, Color.White);

            // TODO remove debug output below:
            Vector2 position2 = new Vector2(hMidPoint - 300, kGameHeight - 45);
            Vector2 position3 = new Vector2(hMidPoint - 150, kGameHeight - 80);
            //Vector2 position5 = new Vector2(hMidPoint - 100, kGameHeight - 100);

            spriteBatch.DrawString(gameFont, ("Hand Position on Screen: " + handPos), position2, Color.White);
            //spriteBatch.DrawString(gameFont, ("Player Mode: " + gameMode), position3, Color.White);
            //spriteBatch.DrawString(gameFont, gameText, position5, Color.White);
        }
    }
}
