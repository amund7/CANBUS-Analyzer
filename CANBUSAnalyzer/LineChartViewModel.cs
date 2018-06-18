using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Ikc5.TypeLibrary;
using OxyPlot;
using WpfApplication.Models;

namespace WpfApplication.ViewModels
{
	public class LineChartViewModel : BaseNotifyPropertyChanged, ILineChartViewModel
	{
		private readonly IChartRepository _chartRepository;

		public LineChartViewModel(IChartRepository chartRepository)
		{
			chartRepository.ThrowIfNull(nameof(chartRepository));
			_chartRepository = chartRepository;
			chartRepository.PropertyChanged += ChartRepositoryPropertyChanged;
		}

		private void ChartRepositoryPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (!nameof(IChartRepository.LineCountList).Equals(e.PropertyName))
				return;

			OnPropertyChanged(nameof(CountList));
		}

		public IReadOnlyList<DataPoint> CountList =>
			_chartRepository.LineCountList.Select((value, index) => new DataPoint(index, value)).ToList();

	}
}
