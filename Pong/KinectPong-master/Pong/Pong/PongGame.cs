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

                //Make sure the hand position is within the boundaries of the 1.0 to -0.5 scaled set
                if (position > 1.0f)
                    return 0;
                if (position < -0.5f)
                    return maxPixel;

                //Change the range so that we can work with the negative values of the hand position
                position += 1;

                //Get the percentage of the hand position within the 0.5 - 0.0 scaled set
                float percent = position / maxSkeleton;

                //Get the new position of the bat using the scaled hand position
                value = (int)(maxPixel - (maxPixel * percent));
            }

            //Return the new postion of the paddle scaled
            return value;
        }
    }


	public class PongGame : Microsoft.Xna.Framework.Game
	{

        const int kLRMargin = 20, kPaddleWidth = 26, kPaddleHeight = 120, kSmallPaddleHeight = 60;
		const int kBallWidth = 24, kBallHeight = 24;
		const int kMaxAIPaddleVelocity = 7;
		const int kGameWidth = 1360, kGameHeight = 800;
		
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

        float handPos;
        int ballRedVelocityX;
        int ballRedVelocityY;

        int ballBlueVelocityX;
        int ballBlueVelocityY;

        string gameText = "";

        DateTime time = new DateTime();

        int gameLevel = 0;
        int gameMode = 0;

        enum playerMode { singlePlayer, multiPlayer };

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

		public PongGame()
		{
			graphics = new GraphicsDeviceManager(this);
			graphics.PreferredBackBufferWidth = kGameWidth;
			graphics.PreferredBackBufferHeight = kGameHeight;
			
			Content.RootDirectory = "Content";
		}
		
		private void RestartGame()
		{
            aiPaddleRectRed = new Rectangle(GraphicsDevice.Viewport.Width - kLRMargin - kPaddleWidth, 20, kPaddleWidth, kPaddleHeight);
            aiPaddleRectBlue = new Rectangle(GraphicsDevice.Viewport.Width - kLRMargin - kPaddleWidth - 60, 20, kPaddleWidth, kPaddleHeight);

            ballRedRect = new Rectangle(500, 600, kBallWidth, kBallHeight);
            ballBlueRect = new Rectangle(500, 300, kBallWidth, kBallHeight);

            if (player1Score >= 3)
            {
                //set text to player 1 wins
                gameText = "Player 1 Wins!";
                ballRedVelocity = new Vector2(0.0f, 0.0f);
                ballBlueVelocity = new Vector2(0.0f, 0.0f);
                //Progress the game and tally "Game" score
                gameLevel++;
                player1GameScore++;
            }
            else if (player2Score >= 3)
            {
                //set text to player 2 wins
                gameText = "AI Wins!";
                ballRedVelocity = new Vector2(0.0f, 0.0f);
                ballBlueVelocity = new Vector2(0.0f, 0.0f);
                //Progress the game and tally "Game" score
                gameLevel++;
                player2GameScore++;
            }
            else
            {
                //randomly create a velocity for the blue ball
                ballBlueVelocity = RandomVelocity();

                if (gameLevel == 3 || gameLevel == 4)
                {
                    //randomly create a velocity for the red ball
                    ballRedVelocity = RandomVelocity();
                }

                //Update red ball velocity to be displayed (testing only)
                ballRedVelocityX = (int)ballRedVelocity.X;
                ballRedVelocityY = (int)ballRedVelocity.Y;
                
                //Update blue ball velocity to be displayed (testing only)
                ballBlueVelocityX = (int)ballBlueVelocity.X;
                ballBlueVelocityY = (int)ballBlueVelocity.Y;
            }

		}

        private Vector2 RandomVelocity()
        {
            Vector2 randomVelocity = new Vector2();

            randomVelocity = new Vector2((float)new Random(time.Millisecond).Next(-10, 10), (float)new Random(time.Millisecond).Next(-10, 10));
            while (randomVelocity.X == 0 || (randomVelocity.X >= -6 && randomVelocity.X <= 0) || (randomVelocity.X <= 6 && randomVelocity.X >= 0))
            {
                randomVelocity.X = new Random().Next(-10, 10);
            }
            while (randomVelocity.Y == 0 || (randomVelocity.Y >= -6 && randomVelocity.Y <= 0) || (randomVelocity.Y <= 6 && randomVelocity.Y >= 0))
            {
                randomVelocity.Y = new Random().Next(-10, 10);
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

                        handPos = jointRight.Position.Y; //for testing purposes, can be deleted for release version   

                        switch (gameLevel)
                        {
                            case 0: 
                                Joint jointHead = skel.Joints[JointType.Head];
                                if (jointRight.Position.Y >= jointHead.Position.Y)
                                {
                                    gameLevel++;
                                }
                                break;
                            //gamelevel 1 = one big
                            case 1: 
                                handRightY = (int)SkeletalCommonExtensions.ScaleTo(jointRight, kGameWidth, kGameHeight - kPaddleHeight, 0.5f, 0.5f, ScalingType.paddleBig).Position.Y;
                                break;
                            //gamelevel 2 = one small
                            case 2:
                                handRightY = (int)SkeletalCommonExtensions.ScaleTo(jointRight, kGameWidth, kGameHeight - kPaddleHeight, 1.0f, 1.0f, ScalingType.paddleSmall).Position.Y;
                                break;
                            //gamelevel 3 = two big
                            case 3:
                                handRightY = (int)SkeletalCommonExtensions.ScaleTo(jointRight, kGameWidth, kGameHeight - kPaddleHeight, 0.5f, 0.5f, ScalingType.paddleBig).Position.Y;
                                handLeftY = (int)SkeletalCommonExtensions.ScaleTo(jointLeft, kGameWidth, kGameHeight - kPaddleHeight, 0.5f, 0.5f, ScalingType.paddleBig).Position.Y;
                                break;
                            //gamelevel 4 = one big, one small
                            case 4:
                                handRightY = (int)SkeletalCommonExtensions.ScaleTo(jointRight, kGameWidth, kGameHeight - kPaddleHeight, 0.5f, 0.5f, ScalingType.paddleBig).Position.Y;
                                handLeftY = (int)SkeletalCommonExtensions.ScaleTo(jointLeft, kGameWidth, kGameHeight - kPaddleHeight, 1.0f, 1.0f, ScalingType.paddleSmall).Position.Y;
                                break;
                            default: break;
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
                        else if (gameMode == (int)playerMode.multiPlayer)
                        {
                            if (skel == skeletons[1])
                            {
                                player2PaddleRectRight.Y = handRightY;
                                player2PaddleRectLeft.Y = handLeftY;
                            }
                        }

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

			dotTexture = Content.Load<Texture2D>("Dot");
			ballTexture = Content.Load<Texture2D>("Ball");
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
				done = (resultRed == BallCollision.RightMiss || resultRed == BallCollision.RightPaddle || resultBlue == BallCollision.RightMiss || resultBlue == BallCollision.LeftMiss);
			}

            predictedBallRedHeight = currentBallRectRed.Y + new Random(time.Millisecond).Next(-15, 15);
            predictedBallBlueHeight = currentBallRectBlue.Y + new Random(time.Millisecond).Next(-15, 15);
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
			}
			else if (enclosingRectRed.Y <= 0)
			{
				velocityRed.Y *= -1;
			}
			
			if (aiPaddleRectRed.Intersects(enclosingRectRed))
			{
				velocityRed.X *= -1;
				collision = BallCollision.RightPaddle;
			}
			else if (player1PaddleRectLeft.Intersects(enclosingRectRed))
			{
				velocityRed.X *= -1;
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
                if (velocityBlue.Y == -0.5)
                {
                    Console.WriteLine("This is wrong v1");
                }
                Console.WriteLine("velocityBlue.Y: " + velocityBlue.Y);
            }
            else if (enclosingRectBlue.Y <= 0)
            {
                velocityBlue.Y *= -1;
                if (velocityBlue.Y == -0.5)
                {
                    Console.WriteLine("This is wrong v2");
                }
                Console.WriteLine("velocityBlue.Y: " + velocityBlue.Y);
            }

            if (aiPaddleRectBlue.Intersects(enclosingRectBlue))
            {
                float oldVelocityX = velocityBlue.X;
                velocityBlue.X *= -1;
                //Checking to make sure the ball doesn't get stuck within the AI's paddle
                if (enclosingRectBlue.X != kGameWidth / 2)
                {
                    if ((velocityBlue.X * -1) == oldVelocityX)
                    {
                        enclosingRectBlue.X -= 20;
                        if (velocityBlue.X > 0)
                        {
                            velocityBlue.X = velocityBlue.X * -1;
                        }
                        else if (velocityBlue.X == 0)
                        {
                            velocityBlue.X = -0.5f;
                        }
                    }
                }
                Console.WriteLine("velocityBlue.X: " + velocityBlue.X);
                collision = BallCollision.RightPaddle;
            }
            else if (player1PaddleRectRight.Intersects(enclosingRectBlue))
            {
                float oldVelocityX = velocityBlue.X;
                velocityBlue.X *= -1;
                //Checking to make sure the ball doesn't get stuck within the player's paddle
                if (enclosingRectBlue.X != kGameWidth / 2)
                {
                    if ((velocityBlue.X * -1) == oldVelocityX)
                    {
                        enclosingRectBlue.X += 20;
                        if (velocityBlue.X > 0)
                        {
                            velocityBlue.X = velocityBlue.X * -1;
                        }
                        else if (velocityBlue.X == 0)
                        {
                            velocityBlue.X = 0.5f;
                        }
                    }
                }
                Console.WriteLine("velocityBlue.Y: " + velocityBlue.Y);
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
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            if (Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.Escape))
                this.Exit();

            if (Keyboard.GetState(PlayerIndex.One).IsKeyDown(Keys.N))
            {
                player1Score = 0;
                player2Score = 0;
                player1GameScore = 0;
                player2GameScore = 0;
                gameLevel = 0;
                gameMode = (int)playerMode.singlePlayer;
                gameText = "";
                RestartGame();
            }

            //need to change this to a switch statement most likely, to add gamelevel mechanics
            if (gameLevel == 0)
            {
            }
            else
            {

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
                }

                if (collisionBlue == BallCollision.RightMiss || collisionBlue == BallCollision.LeftMiss)
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

                if (passedCenter == false && ballRedVelocity.X > 0 && (ballRedRect.X + kBallWidth >= GraphicsDevice.Viewport.Bounds.Center.X))
                {
                    SimulateRestOfTurn();
                    passedCenter = true;
                }

                if (passedCenter == false && ballBlueVelocity.X > 0 && (ballBlueRect.X + kBallWidth >= GraphicsDevice.Viewport.Bounds.Center.X))
                {
                    SimulateRestOfTurn();
                    passedCenter = true;
                }

                int ballCenterRed = (int)predictedBallRedHeight + (kBallHeight / 2);
                int ballCenterBlue = (int)predictedBallBlueHeight + (kBallHeight / 2);
                int aiPaddleCenterRed = aiPaddleRectRed.Center.Y;
                int aiPaddleCenterBlue = aiPaddleRectBlue.Center.Y;

                if (predictedBallRedHeight > 0 && ballCenterRed != aiPaddleCenterRed)
                {
                    if (ballCenterRed < aiPaddleCenterRed)
                    {
                        aiPaddleRectRed.Y -= kMaxAIPaddleVelocity;
                        Console.WriteLine("aiPaddleRecRed.Y: " + aiPaddleRectRed.Y);
                        Console.WriteLine("kMaxAIPaddleVelocity:" + kMaxAIPaddleVelocity);
                    }
                    else if (ballCenterRed > aiPaddleCenterRed)
                    {
                        aiPaddleRectRed.Y += kMaxAIPaddleVelocity;
                        Console.WriteLine("aiPaddleRecRed.Y: " + aiPaddleRectRed.Y);
                        Console.WriteLine("kMaxAIPaddleVelocity:" + kMaxAIPaddleVelocity);
                    }

                    if (Math.Abs(ballCenterRed - aiPaddleCenterRed) < kMaxAIPaddleVelocity)
                    {
                        aiPaddleRectRed.Y = ballCenterRed - (kPaddleHeight / 2);
                        Console.WriteLine("aiPaddleRecRed.Y: " + aiPaddleRectRed.Y);
                        Console.WriteLine("ballCenterRed: " + ballCenterRed);
                    }
                }

                if (predictedBallBlueHeight > 0 && ballCenterBlue != aiPaddleCenterBlue)
                {
                    if (ballCenterBlue < aiPaddleCenterBlue)
                    {
                        aiPaddleRectBlue.Y -= kMaxAIPaddleVelocity;
                        Console.WriteLine("aiPaddleRecBlue.Y: " + aiPaddleRectBlue.Y);
                        Console.WriteLine("kMaxAIPaddleVelocity:" + kMaxAIPaddleVelocity);
                    }
                    else if (ballCenterBlue > aiPaddleCenterBlue)
                    {
                        aiPaddleRectBlue.Y += kMaxAIPaddleVelocity;
                        Console.WriteLine("aiPaddleRecBlue.Y: " + aiPaddleRectBlue.Y);
                        Console.WriteLine("kMaxAIPaddleVelocity:" + kMaxAIPaddleVelocity);
                    }

                    if (Math.Abs(ballCenterBlue - aiPaddleCenterBlue) < kMaxAIPaddleVelocity)
                    {
                        aiPaddleRectBlue.Y = ballCenterBlue - (kPaddleHeight / 2);
                        Console.WriteLine("aiPaddleRecBlue.Y: " + aiPaddleRectBlue.Y);
                        Console.WriteLine("ballCenterBlue: " + ballCenterBlue);
                    }
                }

                base.Update(gameTime);
            }
        }
		
		protected override void Draw(GameTime gameTime)
		{
            GraphicsDevice.Clear(Color.White);

            spriteBatch.Begin();

            if (gameLevel == 0)
            {
                Vector2 titlePostion = new Vector2((kGameWidth/2) - 100, (kGameHeight/2) - 50);
                Vector2 taglinePosition = new Vector2(titlePostion.X - 50, titlePostion.Y + 50);
                spriteBatch.DrawString(titleFont, ("PONG 3000"), titlePostion, Color.Black);
                spriteBatch.DrawString(gameFont, ("Let's Pong It Up! Game Level: " + gameLevel), taglinePosition, Color.Black);
            }
            else
            {
                //skeleton code for multiplayer and singleplayer with gamelevel mechanics
                if (gameMode == (int)playerMode.singlePlayer)
                {
                    //Draw the player's paddles
                    spriteBatch.Draw(dotTexture, player1PaddleRectRight, Color.Blue);

                    //Draw the AI's paddles
                    spriteBatch.Draw(dotTexture, aiPaddleRectBlue, Color.SteelBlue);

                    //Draw the ball
                    spriteBatch.Draw(ballTexture, ballBlueRect, Color.Blue);

                    if (gameLevel == 3 || gameLevel == 4)
                    {
                        //Draw the player's paddles
                        spriteBatch.Draw(dotTexture, player1PaddleRectLeft, Color.Red);

                        //Draw the AI's paddles
                        spriteBatch.Draw(dotTexture, aiPaddleRectRed, Color.IndianRed);

                        //Draw the ball
                        spriteBatch.Draw(ballTexture, ballRedRect, Color.Red);
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
                ////Draw the player's paddles
                //spriteBatch.Draw(dotTexture, player1PaddleRectRight, Color.Blue);
                //spriteBatch.Draw(dotTexture, player1PaddleRectLeft, Color.Red);
                ////Draw the AI's paddles
                //spriteBatch.Draw(dotTexture, aiPaddleRectRed, Color.IndianRed);
                //spriteBatch.Draw(dotTexture, aiPaddleRectBlue, Color.SteelBlue);
                ////Draw the ball
                //spriteBatch.Draw(ballTexture, ballRedRect, Color.Red);
                //spriteBatch.Draw(ballTexture, ballBlueRect, Color.Blue);

                Vector2 position = new Vector2(500.0f, 10.0f);
                Vector2 position2 = new Vector2(500.0f, 30.0f);
                Vector2 position3 = new Vector2(500.0f, 350.0f);
                Vector2 position4 = new Vector2(500.0f, 50.0f);
                Vector2 position5 = new Vector2(500.0f, 70.0f);

                spriteBatch.DrawString(gameFont, (player1Score + " | " + player2Score), position, Color.Black);
                spriteBatch.DrawString(gameFont, ("Hand Position on Screen: " + handPos),position2,Color.Black);
                spriteBatch.DrawString(gameFont, ("Game Level: " + gameLevel), position4, Color.Black);
                spriteBatch.DrawString(gameFont, ("Player Mode: " + gameMode), position5, Color.Black);
                spriteBatch.DrawString(gameFont, gameText, position3, Color.SteelBlue);

            }

			spriteBatch.End();
			
			base.Draw(gameTime);

		}

        private void drawTitleScreen()
        {
            Vector2 titlePostion = new Vector2((kGameWidth / 2) - 100, (kGameHeight / 2) - 50);
            Vector2 taglinePosition = new Vector2(titlePostion.X - 50, titlePostion.Y + 50);
            spriteBatch.DrawString(titleFont, ("PONG 3000"), titlePostion, Color.Black);
            spriteBatch.DrawString(gameFont, ("Let's Pong It Up! Game Level: " + gameLevel), taglinePosition, Color.Black);
        }

        private void drawEndScreen()
        {
            Vector2 titlePostion = new Vector2((kGameWidth / 2) - 100, (kGameHeight / 2) - 50);
            Vector2 taglinePosition = new Vector2(titlePostion.X - 50, titlePostion.Y + 50);
            spriteBatch.DrawString(titleFont, ("PONG 3000"), titlePostion, Color.Black);
            spriteBatch.DrawString(gameFont, ("Winner text"), taglinePosition, Color.Black);
        }
	}
}
