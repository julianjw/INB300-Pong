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

//using Microsoft.Kinect.Toolkit;

namespace Pong
{
    internal static class SkeletalCommonExtensions
    {
        public static Joint ScaleTo(this Joint joint, int width, int height, float skeletonMaxX, float skeletonMaxY, int gameLevel)
        {
            Microsoft.Kinect.SkeletonPoint pos = new SkeletonPoint()
            {
                X = Scale(width, skeletonMaxX, joint.Position.X),
                Y = Scale(height, skeletonMaxY, joint.Position.Y), //-joint.Position.Y
                Z = joint.Position.Z
            };

            joint.Position = pos;

            return joint;
        }

        public static Joint ScaleTo(this Joint joint, int width, int height)
        {
            return ScaleTo(joint, width, height, 1.0f, 1.0f, 1);
        }

        private static float Scale(int maxPixel, float maxSkeleton, float position)
        {
            //position = (float)((((0.5 / 0.25f) / 2) * position) + (0.5 / 2));
            //if (position > 0.5f)
            //    position = 0.5f;
            //if (position < 0)
            //    position = 0;

            //float value = ((((maxPixel / maxSkeleton) / 2) * position) + (maxPixel / 2));
            //if (value > maxPixel)
            //    return maxPixel;
            //if (value < 0)
            //    return 0;
            //return value;

            //Make sure the hand position is within the boundaries of the 0.5 - 0.0 scaled set
            if (position > 0.5f)
                return 0;
            if (position < 0)
                return maxPixel;

            //Make the sure the position is positive so the hand position is being used properly
            //position = Math.Abs(position);

            //Get the percentage of the hand position within the 0.5 - 0.0 scaled set
            float percent = position / maxSkeleton; 

            //Get the new position of the bat using the scaled hand position
            int value = (int)(maxPixel - (maxPixel * percent));

            //Return the new bat position
            return value;
        }
    }


	public class PongGame : Microsoft.Xna.Framework.Game
	{

		const int kLRMargin = 20, kPaddleWidth = 26, kPaddleHeight = 120;
		const int kBallWidth = 24, kBallHeight = 24;
		const int kMaxAIPaddleVelocity = 7;
		const int kGameWidth = 1360, kGameHeight = 800;
		
		bool passedCenter = false;
		
		GraphicsDeviceManager graphics;
		SpriteBatch spriteBatch;
		
		Texture2D dotTexture = null, ballTexture = null;
		
		Rectangle ourPaddleRectRight = new Rectangle(kLRMargin + 60, 0, kPaddleWidth, kPaddleHeight);
        Rectangle ourPaddleRectLeft = new Rectangle(kLRMargin, 0, kPaddleWidth, kPaddleHeight);
		Rectangle aiPaddleRect;
		
		Vector2 ballRedVelocity;
        Vector2 ballBlueVelocity;

		Rectangle ballRedRect;
        Rectangle ballBlueRect;
		
		float predictedBallHeight = 0.0f;

        int player1Score = 0;
        int player2Score = 0;

        SpriteFont gameFont;

        float handPos;
        int ballRedVelocityX;
        int ballRedVelocityY;

        int ballBlueVelocityX;
        int ballBlueVelocityY;

        string gameText = "";

        DateTime time = new DateTime();

        int gameLevel = 1;

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
            aiPaddleRect = new Rectangle(GraphicsDevice.Viewport.Width - kLRMargin - kPaddleWidth, 20, kPaddleWidth, kPaddleHeight);
            ballRedRect = new Rectangle(500, 600, kBallWidth, kBallHeight);
            ballBlueRect = new Rectangle(500, 300, kBallWidth, kBallHeight);

            if (player1Score >= 3)
            {
                //set text to player 1 wins
                gameText = "Player 1 Wins!";
                ballRedVelocity = new Vector2(0.0f, 0.0f);
            }
            else if (player2Score >= 3)
            {
                //set text to player 2 wins
                gameText = "Player 2 Wins!";
                ballRedVelocity = new Vector2(0.0f, 0.0f);
            }
            else
            {
                ballRedVelocity = new Vector2((float)new Random(time.Millisecond).Next(-10, 10), (float)new Random(time.Millisecond).Next(-10, 10));
                while (ballRedVelocity.X == 0 || (ballRedVelocity.X >= -6 && ballRedVelocity.X <= 0) || (ballRedVelocity.X <= 6 && ballRedVelocity.X >= 0)) {
                    ballRedVelocity.X = new Random().Next(-10, 10);
                }
                while (ballRedVelocity.Y == 0 || (ballRedVelocity.Y >= -6 && ballRedVelocity.Y <= 0) || (ballRedVelocity.Y <= 6 && ballRedVelocity.Y >= 0))
                {
                    ballRedVelocity.Y = new Random().Next(-10, 10);
                }
                ballRedVelocityX = (int)ballRedVelocity.X;
                ballRedVelocityY = (int)ballRedVelocity.Y;
            }

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

                        //need to add in left hand stuff

                        int handLeftY = 0;

                        //right hand = red paddle and ball
                        
                        handRightY = (int)SkeletalCommonExtensions.ScaleTo(jointRight, kGameWidth, kGameHeight - kPaddleHeight, 0.5f, 0.5f, gameLevel).Position.Y;

                        //left hand = blue paddle and ball

                        handLeftY = (int)SkeletalCommonExtensions.ScaleTo(jointLeft, kGameWidth, kGameHeight - kPaddleHeight, 0.5f, 0.5f, gameLevel).Position.Y;

                        //if multiplayer blah blah blah

                        handPos = jointRight.Position.Y;

                        ourPaddleRectRight.Y = handRightY;

                        ourPaddleRectLeft.Y = handLeftY;

                        break;
                    }
                }
            }
        }

		protected override void LoadContent()
		{
			spriteBatch = new SpriteBatch(GraphicsDevice);

            gameFont = Content.Load<SpriteFont>("Scoreboard");

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
			Rectangle currentBallRect = ballRedRect;
			Vector2 currentballRedVelocity = ballRedVelocity;
			
			bool done = false;
			
			while (!done)
			{
				BallCollision result = AdjustBallPositionWithScreenBounds(ref currentBallRect, ref currentballRedVelocity);
				done = (result == BallCollision.RightMiss || result == BallCollision.RightPaddle);
			}
			
			predictedBallHeight = currentBallRect.Y;
		}
		
		enum BallCollision
		{
			None,
			RightPaddle,
			LeftPaddle,
			RightMiss,
			LeftMiss
		}
		
		private BallCollision AdjustBallPositionWithScreenBounds(ref Rectangle enclosingRect, ref Vector2 velocity)
		{
			BallCollision collision = BallCollision.None;
			
			enclosingRect.X += (int)velocity.X;
			enclosingRect.Y += (int)velocity.Y;
			
			if (enclosingRect.Y >= GraphicsDevice.Viewport.Height - kBallHeight)
			{
				velocity.Y *= -1;
			}
			else if (enclosingRect.Y <= 0)
			{
				velocity.Y *= -1;
			}
			
			if (aiPaddleRect.Intersects(enclosingRect))
			{
				velocity.X *= -1;
				collision = BallCollision.RightPaddle;
			}
			else if (ourPaddleRectLeft.Intersects(enclosingRect))
			{
				velocity.X *= -1;
				collision = BallCollision.LeftPaddle;
			}
			else if (enclosingRect.X >= GraphicsDevice.Viewport.Width - kBallWidth)
			{
				collision = BallCollision.RightMiss;
			}
			else if (enclosingRect.X <= 0)
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
                gameText = "";
                RestartGame();
            }

            
			
			BallCollision collision = AdjustBallPositionWithScreenBounds(ref ballRedRect, ref ballRedVelocity);
			
			if (collision > 0)
			{
				passedCenter = false;
				
				float newY = (new Random().Next(80) + 1) / 10.0f;
				ballRedVelocity.Y = ballRedVelocity.Y > 0 ? newY : -newY;
			}
			
			if (collision == BallCollision.RightMiss || collision == BallCollision.LeftMiss)
			{
                //Changes the score to reflect who won the point (who missed the ball)
                if (BallCollision.RightMiss == collision) {
                    player1Score += 1;
                } else if (BallCollision.LeftMiss == collision) {
                    player2Score += 1;
                }

				RestartGame();
			}
			
			if (passedCenter == false && ballRedVelocity.X > 0 && (ballRedRect.X + kBallWidth >= GraphicsDevice.Viewport.Bounds.Center.X))
			{
				SimulateRestOfTurn();
				passedCenter = true;
			}
			
			int ballCenter = (int)predictedBallHeight + (kBallHeight / 2);
			int aiPaddleCenter = aiPaddleRect.Center.Y;
			
			if (predictedBallHeight > 0 && ballCenter != aiPaddleCenter)
			{
				if (ballCenter < aiPaddleCenter)
				{
					aiPaddleRect.Y -= kMaxAIPaddleVelocity;
				}
				else if (ballCenter > aiPaddleCenter)
				{
					aiPaddleRect.Y += kMaxAIPaddleVelocity;
				}
				
				if (Math.Abs(ballCenter - aiPaddleCenter) < kMaxAIPaddleVelocity)
				{
					aiPaddleRect.Y = ballCenter - (kPaddleHeight / 2);
				}
			}
			
			base.Update(gameTime);
		}
		
		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.White);
			
			spriteBatch.Begin();
			
            //Draw the player's paddles
			spriteBatch.Draw(dotTexture, ourPaddleRectRight, Color.Blue);
            spriteBatch.Draw(dotTexture, ourPaddleRectLeft, Color.Red);
            //Draw the AI's paddles
			spriteBatch.Draw(dotTexture, aiPaddleRect, Color.IndianRed);
            //Draw the ball
			spriteBatch.Draw(ballTexture, ballRedRect, Color.Red);

            Vector2 position = new Vector2(500.0f, 10.0f);
            Vector2 position2 = new Vector2(500.0f, 30.0f);
            Vector2 position3 = new Vector2(500.0f, 300.0f);
            Vector2 position4 = new Vector2(500.0f, 50.0f);
            Vector2 position5 = new Vector2(500.0f, 70.0f);

            spriteBatch.DrawString(gameFont, (player1Score + " | " + player2Score), position, Color.Black);
            spriteBatch.DrawString(gameFont, ("Hand Position on Screen: " + handPos),position2,Color.Black);
            spriteBatch.DrawString(gameFont, ("Ball Velocity X: " + ballRedVelocityX), position4, Color.Black);
            spriteBatch.DrawString(gameFont, ("Ball Velocity Y: " + ballRedVelocityY), position5, Color.Black);
            spriteBatch.DrawString(gameFont, gameText, position3, Color.SteelBlue);
			
			spriteBatch.End();
			
			base.Draw(gameTime);
		}
	}
}
