using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API.Internals;

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class FVG : Indicator
    {
        [Parameter("看涨颜色", DefaultValue = "DodgerBlue", Group = "FVG 显示")]
        public string BullishColorString { get; set; }

        [Parameter("看跌颜色", DefaultValue = "Tomato", Group = "FVG 显示")]
        public string BearishColorString { get; set; }

        [Parameter("已回补颜色", DefaultValue = "Gray", Group = "FVG 显示")]
        public string MitigatedColorString { get; set; }

        [Parameter("填充方框", DefaultValue = true, Group = "FVG 显示")]
        public bool FillBoxes { get; set; }

        [Parameter("延长方框", DefaultValue = true, Group = "FVG 显示")]
        public bool ExtendBoxes { get; set; }

        [Parameter("方框透明度", DefaultValue = 50, MinValue = 0, MaxValue = 255, Group = "FVG 显示")]
        public int BoxOpacity { get; set; }
        
        [Parameter("隐藏已回补FVG", DefaultValue = true, Group = "FVG 行为")]
        public bool HideMitigated { get; set; }

        [Parameter("最大显示FVG数量", DefaultValue = 10, MinValue = 1, Group = "FVG 行为")]
        public int MaxFvgsToShow { get; set; }
        
        [Parameter("最小FVG尺寸 (点)", DefaultValue = 0.0, MinValue = 0, Group = "FVG 行为")]
        public double MinFvgSize { get; set; }

        [Parameter("显示日线", DefaultValue = true, Group = "日线")]
        public bool ShowDailyLines { get; set; }

        [Parameter("交易日重置小时 (UTC)", DefaultValue = 22, MinValue = 0, MaxValue = 23, Group = "日线")]
        public int SessionResetHour { get; set; }
        
        [Parameter("高点线颜色", DefaultValue = "Green", Group = "日线")]
        public string HighLineColorString { get; set; }

        [Parameter("低点线颜色", DefaultValue = "Red", Group = "日线")]
        public string LowLineColorString { get; set; }

        [Parameter("线段粗细", DefaultValue = 1, MinValue = 1, MaxValue = 5, Group = "日线")]
        public int DailyLineThickness { get; set; }

        [Parameter("线段样式", DefaultValue = LineStyle.Dots, Group = "日线")]
        public LineStyle DailyLineStyle { get; set; }

        [Parameter("--- 订单块 (OB) ---", Group = "订单块 显示")]
        public bool Separator1 { get; set; }

        [Parameter("显示订单块", DefaultValue = true, Group = "订单块 显示")]
        public bool ShowOrderBlocks { get; set; }

        [Parameter("看涨OB颜色", DefaultValue = "Aqua", Group = "订单块 显示")]
        public string BullishObColorString { get; set; }

        [Parameter("看跌OB颜色", DefaultValue = "Magenta", Group = "订单块 显示")]
        public string BearishObColorString { get; set; }

        [Parameter("已回补OB颜色", DefaultValue = "Gray", Group = "订单块 显示")]
        public string MitigatedObColorString { get; set; }

        [Parameter("填充OB方框", DefaultValue = true, Group = "订单块 显示")]
        public bool ObFillBoxes { get; set; }

        [Parameter("延长OB方框", DefaultValue = true, Group = "订单块 显示")]
        public bool ObExtendBoxes { get; set; }

        [Parameter("OB方框透明度", DefaultValue = 50, MinValue = 0, MaxValue = 255, Group = "订单块 显示")]
        public int ObOpacity { get; set; }

        [Parameter("隐藏已回补OB", DefaultValue = true, Group = "订单块 行为")]
        public bool HideMitigatedObs { get; set; }

        [Parameter("结构查找周期", DefaultValue = 10, MinValue = 2, Group = "订单块 行为")]
        public int StructureLookback { get; set; }

        [Parameter("--- OB 过滤器 ---", Group = "订单块 行为")]
        public bool Separator2 { get; set; }

        [Parameter("启用成交量过滤", DefaultValue = true, Group = "订单块 行为")]
        public bool FilterByVolume { get; set; }

        [Parameter("成交量查找周期", DefaultValue = 20, MinValue = 2, Group = "订单块 行为")]
        public int VolumeLookback { get; set; }

        [Parameter("成交量倍数", DefaultValue = 1.5, MinValue = 1.0, Group = "订单块 行为")]
        public double VolumeMultiplier { get; set; }

        [Parameter("启用失衡区过滤", DefaultValue = true, Group = "订单块 行为")]
        public bool FilterByImbalance { get; set; }

        // --- Private FVG Variables ---
        private Color _bullishColor;
        private Color _bearishColor;
        private Color _mitigatedColor;
        private readonly List<FairValueGap> _activeFvgs = new List<FairValueGap>();
        private const string FvgObjectPrefix = "FVG_";

        // --- Private OB Variables ---
        private Color _bullishObColor;
        private Color _bearishObColor;
        private Color _mitigatedObColor;
        private readonly List<OrderBlock> _activeObs = new List<OrderBlock>();
        private const string ObObjectPrefix = "OB_";

        // --- Private Daily Lines Variables ---
        private double _dailyHigh;
        private double _dailyLow;
        private DateTime _currentSessionDate;
        private DateTime _sessionStartBarTime;
        private Color _highLineColor;
        private Color _lowLineColor;
        private const string DailyHighName = "DailyHighLine";
        private const string DailyLowName = "DailyLowLine";

        // --- FVG Data Structure ---
        private class FairValueGap
        {
            public double TopPrice { get; set; }
            public double BottomPrice { get; set; }
            public double OriginalTopPrice { get; set; }
            public double OriginalBottomPrice { get; set; }
            public bool IsBullish { get; set; }
            public int StartBarIndex { get; set; }
            public string RectangleName { get; set; }
            public bool IsMitigated { get; set; }
            public int MitigatedBarIndex { get; set; }
        }

        // --- OB Data Structure ---
        private class OrderBlock
        {
            public double HighPrice { get; set; }
            public double LowPrice { get; set; }
            public bool IsBullish { get; set; }
            public int BarIndex { get; set; }
            public string RectangleName { get; set; }
            public bool IsMitigated { get; set; }
            public int MitigatedBarIndex { get; set; }
            public bool IsConfirmed { get; set; }
        }

        protected override void Initialize()
        {
            _bullishColor = Color.FromName(BullishColorString);
            _bearishColor = Color.FromName(BearishColorString);
            _mitigatedColor = Color.FromName(MitigatedColorString);
            _highLineColor = Color.FromName(HighLineColorString);
            _lowLineColor = Color.FromName(LowLineColorString);
            
            _bullishObColor = Color.FromName(BullishObColorString);
            _bearishObColor = Color.FromName(BearishObColorString);
            _mitigatedObColor = Color.FromName(MitigatedObColorString);
        }

        public override void Calculate(int index)
        {
            UpdateDailyLines(index);
            
            // Clear all drawings at the start of each tick for a clean redraw.
            var allRects = Chart.FindAllObjects<ChartRectangle>();
            foreach (var rect in allRects)
            {
                if (rect.Name.StartsWith(FvgObjectPrefix) || rect.Name.StartsWith(ObObjectPrefix))
                {
                    Chart.RemoveObject(rect.Name);
                }
            }

            DetectAndProcessFvgs(index);

            if (ShowOrderBlocks)
            {
                DetectAndConfirmOrderBlocks(index);
                ProcessOrderBlocks(index);
            }
        }

        private void DetectAndProcessFvgs(int currentIndex)
        {
            // --- FVG Detection on closed bars ---
            if (currentIndex >= 3)
            {
                int processingIndex = currentIndex - 1;
                int fvgStartBarIndex = processingIndex - 1;
                
                if (!_activeFvgs.Any(f => f.StartBarIndex == fvgStartBarIndex))
                {
                    var bar1 = Bars[processingIndex - 2];
                    var bar3 = Bars[processingIndex];

                    if (bar1.High < bar3.Low && (bar3.Low - bar1.High) / Symbol.TickSize >= MinFvgSize)
                    {
                        _activeFvgs.Add(new FairValueGap
                        {
                            TopPrice = bar3.Low, BottomPrice = bar1.High,
                            OriginalTopPrice = bar3.Low, OriginalBottomPrice = bar1.High,
                            IsBullish = true, StartBarIndex = fvgStartBarIndex,
                            RectangleName = $"{FvgObjectPrefix}Bull_{processingIndex}", IsMitigated = false
                        });
                    }
                    else if (bar1.Low > bar3.High && (bar1.Low - bar3.High) / Symbol.TickSize >= MinFvgSize)
                    {
                        _activeFvgs.Add(new FairValueGap
                        {
                            TopPrice = bar1.Low, BottomPrice = bar3.High,
                            OriginalTopPrice = bar1.Low, OriginalBottomPrice = bar3.High,
                            IsBullish = false, StartBarIndex = fvgStartBarIndex,
                            RectangleName = $"{FvgObjectPrefix}Bear_{processingIndex}", IsMitigated = false
                        });
                    }
                }
            }

            // --- FVG State Update & Drawing ---
            if (currentIndex > 0)
            {
                int mitigationBarIndex = currentIndex - 1;
                var mitigationBar = Bars[mitigationBarIndex];

                foreach (var fvg in _activeFvgs.Where(f => !f.IsMitigated && mitigationBarIndex > f.StartBarIndex + 1))
                {
                    if (fvg.IsBullish)
                    {
                        if (mitigationBar.Low < fvg.TopPrice)
                        {
                            double mitigationLevel = fvg.OriginalBottomPrice + (fvg.OriginalTopPrice - fvg.OriginalBottomPrice) * 0.1;
                            if (mitigationBar.Low <= mitigationLevel)
                            {
                                fvg.IsMitigated = true;
                                fvg.MitigatedBarIndex = mitigationBarIndex;
                            }
                            else
                            {
                                fvg.TopPrice = mitigationBar.Low;
                            }
                        }
                    }
                    else // Bearish
                    {
                        if (mitigationBar.High > fvg.BottomPrice)
                        {
                            double mitigationLevel = fvg.OriginalTopPrice - (fvg.OriginalTopPrice - fvg.OriginalBottomPrice) * 0.1;
                            if (mitigationBar.High >= mitigationLevel)
                            {
                                fvg.IsMitigated = true;
                                fvg.MitigatedBarIndex = mitigationBarIndex;
                            }
                            else
                            {
                                fvg.BottomPrice = mitigationBar.High;
                            }
                        }
                    }
                }
            }
            
            var fvgsToDisplay = _activeFvgs
                .Where(fvg => !fvg.IsMitigated || !HideMitigated)
                .OrderByDescending(fvg => fvg.StartBarIndex)
                .Take(MaxFvgsToShow)
                .ToList();

            foreach (var fvg in fvgsToDisplay)
            {
                DrawFvg(fvg);
            }

            if (_activeFvgs.Count > MaxFvgsToShow * 2)
            {
                var cutoffIndex = _activeFvgs.OrderByDescending(f => f.StartBarIndex).Skip(MaxFvgsToShow).First().StartBarIndex;
                _activeFvgs.RemoveAll(fvg => fvg.IsMitigated && fvg.StartBarIndex < cutoffIndex);
            }
        }

        private void DrawFvg(FairValueGap fvg)
        {
            var color = fvg.IsMitigated ? _mitigatedColor : (fvg.IsBullish ? _bullishColor : _bearishColor);
            var finalColor = Color.FromArgb(BoxOpacity, color);
            var startTime = Bars.OpenTimes[fvg.StartBarIndex];
            DateTime endTime;

            if (fvg.IsMitigated)
            {
                endTime = fvg.MitigatedBarIndex < Bars.Count ? Bars.OpenTimes[fvg.MitigatedBarIndex] : Server.Time;
            }
            else if (ExtendBoxes)
            {
                endTime = Server.Time;
            }
            else
            {
                endTime = fvg.StartBarIndex + 2 < Bars.Count ? Bars.OpenTimes[fvg.StartBarIndex + 2] : Server.Time;
            }

            Chart.DrawRectangle(fvg.RectangleName, startTime, fvg.TopPrice, endTime, fvg.BottomPrice, finalColor, 1, LineStyle.Solid);
            var newRect = Chart.FindObject(fvg.RectangleName) as ChartRectangle;
            if (newRect != null)
            {
                newRect.IsFilled = FillBoxes;
            }
        }

        private void UpdateDailyLines(int index)
        {
            Chart.RemoveObject(DailyHighName);
            Chart.RemoveObject(DailyLowName);
            if (!ShowDailyLines) return;

            var currentBar = Bars[index];
            var sessionDate = GetSessionDate(currentBar.OpenTime, SessionResetHour);

            if (sessionDate != _currentSessionDate)
            {
                _currentSessionDate = sessionDate;
                _dailyHigh = currentBar.High;
                _dailyLow = currentBar.Low;
                _sessionStartBarTime = currentBar.OpenTime;
            }
            else
            {
                _dailyHigh = Math.Max(_dailyHigh, currentBar.High);
                _dailyLow = Math.Min(_dailyLow, currentBar.Low);
            }

            var lineEndTime = Server.Time;
            Chart.DrawTrendLine(DailyHighName, _sessionStartBarTime, _dailyHigh, lineEndTime, _dailyHigh, _highLineColor, DailyLineThickness, DailyLineStyle);
            Chart.DrawTrendLine(DailyLowName, _sessionStartBarTime, _dailyLow, lineEndTime, _dailyLow, _lowLineColor, DailyLineThickness, DailyLineStyle);
        }

        private void DetectAndConfirmOrderBlocks(int currentIndex)
        {
            if (currentIndex < StructureLookback + 2) return;

            int processingIndex = currentIndex - 1;
            var lastHighPoint = FindLastSwingPoint(processingIndex - 1, StructureLookback, true);
            var lastLowPoint = FindLastSwingPoint(processingIndex - 1, StructureLookback, false);

            if (lastHighPoint.Index == -1 || lastLowPoint.Index == -1) return;

            var currentBar = Bars[processingIndex];

            if (currentBar.Low < lastLowPoint.Price) // Bearish BoS
            {
                for (int i = processingIndex; i > lastHighPoint.Index; i--)
                {
                    if (Bars[i].Close > Bars[i].Open) // Find up-candle
                    {
                        if (PassesObFilters(i, false))
                        {
                            AddOrderBlock(i, false);
                            break;
                        }
                    }
                }
            }
            else if (currentBar.High > lastHighPoint.Price) // Bullish BoS
            {
                for (int i = processingIndex; i > lastLowPoint.Index; i--)
                {
                    if (Bars[i].Close < Bars[i].Open) // Find down-candle
                    {
                        if (PassesObFilters(i, true))
                        {
                            AddOrderBlock(i, true);
                            break;
                        }
                    }
                }
            }
        }

        private bool PassesObFilters(int barIndex, bool isBullishOb)
        {
            bool isVolumeValid = !FilterByVolume || IsVolumeValid(barIndex);
            bool hasImbalance = !FilterByImbalance || HasImbalance(barIndex, isBullishOb);
            return isVolumeValid && hasImbalance;
        }

        private void AddOrderBlock(int barIndex, bool isBullish)
        {
            if (!_activeObs.Any(ob => ob.BarIndex == barIndex))
            {
                _activeObs.Add(new OrderBlock
                {
                    HighPrice = Bars[barIndex].High,
                    LowPrice = Bars[barIndex].Low,
                    BarIndex = barIndex,
                    IsBullish = isBullish,
                    RectangleName = $"{ObObjectPrefix}{(isBullish ? "Bull" : "Bear")}_{barIndex}",
                    IsMitigated = false,
                    IsConfirmed = true
                });
            }
        }

        private void ProcessOrderBlocks(int currentIndex)
        {
            if (currentIndex > 0)
            {
                int mitigationBarIndex = currentIndex - 1;
                var mitigationBar = Bars[mitigationBarIndex];
                foreach (var ob in _activeObs.Where(o => !o.IsMitigated && mitigationBarIndex > o.BarIndex))
                {
                    if ((ob.IsBullish && mitigationBar.Low <= ob.HighPrice) || (!ob.IsBullish && mitigationBar.High >= ob.LowPrice))
                    {
                        ob.IsMitigated = true;
                        ob.MitigatedBarIndex = mitigationBarIndex;
                    }
                }
            }

            var obsToDisplay = _activeObs
                .Where(ob => !ob.IsMitigated || !HideMitigatedObs)
                .OrderByDescending(ob => ob.BarIndex)
                .ToList();

            foreach (var ob in obsToDisplay)
            {
                DrawOrderBlock(ob);
            }

            if (_activeObs.Count > 50)
            {
                _activeObs.RemoveAll(ob => ob.IsMitigated && currentIndex - ob.BarIndex > 500);
            }
        }

        private void DrawOrderBlock(OrderBlock ob)
        {
            var color = ob.IsMitigated ? _mitigatedObColor : (ob.IsBullish ? _bullishObColor : _bearishObColor);
            var finalColor = Color.FromArgb(ObOpacity, color);
            var startTime = Bars.OpenTimes[ob.BarIndex];
            DateTime endTime;

            if (ob.IsMitigated)
            {
                endTime = ob.MitigatedBarIndex < Bars.Count ? Bars.OpenTimes[ob.MitigatedBarIndex] : Server.Time;
            }
            else if (ObExtendBoxes)
            {
                endTime = Server.Time;
            }
            else
            {
                endTime = ob.BarIndex + 1 < Bars.Count ? Bars.OpenTimes[ob.BarIndex + 1] : Server.Time;
            }

            Chart.DrawRectangle(ob.RectangleName, startTime, ob.HighPrice, endTime, ob.LowPrice, finalColor, 1, LineStyle.Solid);
            var newRect = Chart.FindObject(ob.RectangleName) as ChartRectangle;
            if (newRect != null)
            {
                newRect.IsFilled = ObFillBoxes;
            }
        }

        private (int Index, double Price) FindLastSwingPoint(int endIndex, int lookback, bool findHigh)
        {
            if (endIndex - lookback < 1) return (-1, 0);

            double swingPointPrice = findHigh ? double.MinValue : double.MaxValue;
            int swingPointIndex = -1;

            for (int i = endIndex; i >= endIndex - lookback; i--)
            {
                if (findHigh)
                {
                    if (Bars[i].High > swingPointPrice)
                    {
                        swingPointPrice = Bars[i].High;
                        swingPointIndex = i;
                    }
                }
                else
                {
                    if (Bars[i].Low < swingPointPrice)
                    {
                        swingPointPrice = Bars[i].Low;
                        swingPointIndex = i;
                    }
                }
            }

            if (swingPointIndex <= 0 || swingPointIndex >= Bars.Count - 1)
            {
                return (-1, 0);
            }

            bool isSwing = false;
            if (findHigh)
            {
                if (Bars[swingPointIndex].High > Bars[swingPointIndex - 1].High && Bars[swingPointIndex].High > Bars[swingPointIndex + 1].High)
                {
                    isSwing = true;
                }
            }
            else
            {
                if (Bars[swingPointIndex].Low < Bars[swingPointIndex - 1].Low && Bars[swingPointIndex].Low < Bars[swingPointIndex + 1].Low)
                {
                    isSwing = true;
                }
            }

            return isSwing ? (swingPointIndex, swingPointPrice) : (-1, 0);
        }

        private bool IsVolumeValid(int barIndex)
        {
            if (barIndex < VolumeLookback) return false;
            double totalVolume = 0;
            for (int i = 1; i <= VolumeLookback; i++)
            {
                totalVolume += Bars.TickVolumes[barIndex - i];
            }
            double averageVolume = totalVolume / VolumeLookback;
            return Bars.TickVolumes[barIndex] > averageVolume * VolumeMultiplier;
        }

        private bool HasImbalance(int obBarIndex, bool isBullishOb)
        {
            if (obBarIndex + 3 >= Bars.Count) return false;
            var bar1 = Bars[obBarIndex + 1];
            var bar3 = Bars[obBarIndex + 3];
            if (isBullishOb)
            {
                return bar1.High < bar3.Low;
            }
            else
            {
                return bar1.Low > bar3.High;
            }
        }

        private DateTime GetSessionDate(DateTime time, int resetHour)
        {
            return time.Hour < resetHour ? time.Date.AddDays(-1) : time.Date;
        }
    }
}