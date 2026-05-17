using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace NSMB.Entities.Player {
    /// <summary>
    /// Simple cloud buddy movement system using frame-based delays.
    /// No smoothing, no splines - just delayed position following.
    /// </summary>
    public class CloudBuddySplineMovement {
        
        private readonly Queue<UnityEngine.Vector3> positionHistory = new(15);
        private const int CLOUD_BUDDY_1_DELAY = 5;   // 5 frames delay
        private const int CLOUD_BUDDY_2_DELAY = 10;  // 10 frames delay
        private const int CLOUD_BUDDY_3_DELAY = 15;  // 15 frames delay
        private const int MAX_HISTORY = 15;
        
        /// <summary>
        /// Update position history and calculate buddy positions
        /// </summary>
        public void UpdateCloudBuddyPositions(UnityEngine.Vector3 marioCurrentPos, 
            Transform buddy1Transform, Transform buddy2Transform, Transform buddy3Transform) {
            
            // Add current position to history
            positionHistory.Enqueue(marioCurrentPos);
            if (positionHistory.Count > MAX_HISTORY) {
                positionHistory.Dequeue();
            }
            
            // Get historical positions for each buddy
            UnityEngine.Vector3 buddy1Pos = GetHistoricalPosition(CLOUD_BUDDY_1_DELAY);
            UnityEngine.Vector3 buddy2Pos = GetHistoricalPosition(CLOUD_BUDDY_2_DELAY);
            UnityEngine.Vector3 buddy3Pos = GetHistoricalPosition(CLOUD_BUDDY_3_DELAY);
            
            // Apply positions
            buddy1Transform.position = buddy1Pos;
            buddy2Transform.position = buddy2Pos;
            buddy3Transform.position = buddy3Pos;
        }
        
        private UnityEngine.Vector3 GetHistoricalPosition(int framesBack) {
            if (positionHistory.Count == 0) {
                return UnityEngine.Vector3.zero;
            }

            int index = positionHistory.Count - framesBack;
            if (index < 0) {
                // Not enough history yet, return the oldest position we have
                return positionHistory.Peek();
            }

            return positionHistory.ElementAt(index);
        }
        
        public void ClearHistory() {
            positionHistory.Clear();
        }
    }
}