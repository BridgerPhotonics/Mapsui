#if DEBUG
//#define CLOCK_FETCH
#endif
using System.Collections.Generic;
using System.Diagnostics;

namespace Mapsui.Fetcher
{
    public class FetchMachine
    {
        private readonly List<FetchWorker> _worker = new List<FetchWorker>();
        
        public FetchMachine(IFetchDispatcher fetchDispatcher, int numberOfWorkers = 4)
        {
            _fetch = fetchDispatcher as TileFetchDispatcher;
            for (int i = 0; i < numberOfWorkers; i++)
            {
                _worker.Add(new FetchWorker(fetchDispatcher));
            }
        }

    #if CLOCK_FETCH
        protected Stopwatch _statUpdate = new Stopwatch();
    #endif
        protected TileFetchDispatcher _fetch = null;

        public void Start()
        {
        #if CLOCK_FETCH
            if(_fetch != null && (_statUpdate.ElapsedMilliseconds >= 5000 || !_statUpdate.IsRunning))
            {
                var avg = _fetch.StatAvg / 1000.0;
                var max = _fetch.StatMax / 1000.0;
                var name = "FetchMachine";
                Debug.WriteLine("Tile avg: {1:N3} max: {2:N3} - {0}", name, avg, max);
                _statUpdate.Restart();
            }
        #endif

            foreach (var worker in _worker)
            {
                worker.Start();
            }
        }
        
        public void Stop()
        {
            foreach (var worker in _worker)
            {
                worker.Stop();
            }
        }
    }
}
