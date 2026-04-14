using System.Threading;

namespace MultiplayerARPG
{
    public class GameEntityCancellationTokenSource<T> : CancellationTokenSource
        where T : BaseGameEntity
    {
        public T Entity { get; private set; }
        public GameEntityCancellationTokenSource(T entity)
        {
            Entity = entity;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                Entity = null;
        }
    }
}