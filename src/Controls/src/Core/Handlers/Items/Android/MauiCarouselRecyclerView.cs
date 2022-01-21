﻿using System;
using System.Collections.Generic;
using Android.Content;
using AndroidX.RecyclerView.Widget;
using Android.Views;

namespace Microsoft.Maui.Controls.Handlers.Items
{
	public class MauiCarouselRecyclerView : MauiRecyclerView<CarouselView, ItemsViewAdapter<CarouselView, IItemsViewSource>, IItemsViewSource>, IMauiCarouselRecyclerView
	{
		ItemDecoration _itemDecoration;
		CarouselViewLoopManager _carouselViewLoopManager;
		int _oldPosition;
		int _gotoPosition = -1;
		bool _noNeedForScroll;
		bool _initialized;
		bool _isVisible;
		bool _disposed;

		List<View> _oldViews;
		CarouselViewwOnGlobalLayoutListener _carouselViewLayoutListener;

		protected CarouselView Carousel => ItemsView;

		public MauiCarouselRecyclerView(Context context, Func<IItemsLayout> getItemsLayout, Func<ItemsViewAdapter<CarouselView, IItemsViewSource>> getAdapter) : base(context, getItemsLayout, getAdapter)
		{
			_oldViews = new List<View>();
			_carouselViewLoopManager = new CarouselViewLoopManager();
		}

		public bool IsSwipeEnabled { get; set; }

		public override bool OnInterceptTouchEvent(MotionEvent ev)
		{
			if (!IsSwipeEnabled)
				return false;

			return base.OnInterceptTouchEvent(ev);
		}

		protected virtual bool IsHorizontal => (Carousel?.ItemsLayout)?.Orientation == ItemsLayoutOrientation.Horizontal;

		protected override int DetermineTargetPosition(ScrollToRequestEventArgs args)
		{
			if (args.Mode == ScrollToMode.Element)
				return ItemsViewAdapter.GetPositionForItem(args.Item);

			if (!Carousel.Loop)
				return args.Index;

			if (_carouselViewLoopManager == null)
				return -1;

			var carouselPosition = GetCarouselViewCurrentIndex(Carousel.Position);
			var getGoIndex = _carouselViewLoopManager.GetGoToIndex(this, carouselPosition, args.Index);

			return getGoIndex;
		}

		public override bool OnTouchEvent(MotionEvent e)
		{
			if (Carousel.Loop)
				_carouselViewLoopManager.CenterIfNeeded(this, IsHorizontal);

			return base.OnTouchEvent(e);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !_disposed)
			{
				_disposed = true;
				_carouselViewLoopManager?.SetItemsSource(null);
				_carouselViewLoopManager = null;

				if (_itemDecoration != null)
				{
					_itemDecoration.Dispose();
					_itemDecoration = null;
				}

				ClearLayoutListener();
			}

			base.Dispose(disposing);
		}

		public override void SetUpNewElement(CarouselView newElement)
		{
			base.SetUpNewElement(newElement);

			AddLayoutListener();
			UpdateItemSpacing();
			UpdateInitialPosition();
		}

		protected override RecyclerViewScrollListener<CarouselView, IItemsViewSource> CreateScrollListener()
			=> new CarouselViewOnScrollListener(ItemsView, ItemsViewAdapter, _carouselViewLoopManager);


		public override void TearDownOldElement(CarouselView oldElement)
		{
			if (ItemsView != null)
				ItemsView.Scrolled -= CarouselViewScrolled;

			ClearLayoutListener();
			base.TearDownOldElement(oldElement);
		}

		public override void UpdateAdapter()
		{
			// By default the CollectionViewAdapter creates the items at whatever size the template calls for
			// But for the Carousel, we want it to create the items to fit the width/height of the viewport
			// So we give it an alternate delegate for creating the views

			var oldItemViewAdapter = ItemsViewAdapter;
			UnsubscribeCollectionItemsSourceChanged(oldItemViewAdapter);
			if (oldItemViewAdapter != null)
			{
				ItemsView.SetValueFromRenderer(CarouselView.PositionProperty, 0);
				ItemsView.SetValueFromRenderer(CarouselView.CurrentItemProperty, null);
			}

			ItemsViewAdapter = CreateAdapter();

			_gotoPosition = -1;

			SwapAdapter(ItemsViewAdapter, false);

			UpdateInitialPosition();

			if (ItemsViewAdapter.ItemsSource is ObservableItemsSource observableItemsSource)
				observableItemsSource.CollectionItemsSourceChanged += CollectionItemsSourceChanged;

			oldItemViewAdapter?.Dispose();
		}

		public override void UpdateItemsSource()
		{
			UpdateAdapter();
			UpdateEmptyView();
			_carouselViewLoopManager.SetItemsSource(ItemsViewAdapter.ItemsSource);
		}

		protected override ItemDecoration CreateSpacingDecoration(IItemsLayout itemsLayout)
		{
			return new CarouselSpacingItemDecoration(itemsLayout, Carousel);
		}

		protected override void UpdateItemSpacing()
		{
			if (ItemsLayout == null)
			{
				return;
			}

			UpdateItemDecoration();

			var adapter = GetAdapter();

			if (adapter != null)
			{
				adapter.NotifyItemChanged(_oldPosition);
			}

			base.UpdateItemSpacing();
		}

		public override void ScrollTo(ScrollToRequestEventArgs args)
		{
			var position = DetermineTargetPosition(args);

			if (_carouselViewLoopManager == null)
				return;

			// Special case here
			// We could have a race condition where we are scrolling our collection to center the first item
			// And at the same time the user is requesting we go to a particular item
			if (position == -1)
			{
				if (Carousel.Loop)
					_carouselViewLoopManager.AddPendingScrollTo(args);

				return;
			}

			if (args.IsAnimated)
			{
				ScrollHelper.AnimateScrollToPosition(position, args.ScrollToPosition);
			}
			else
			{
				ScrollHelper.JumpScrollToPosition(position, args.ScrollToPosition);
			}
		}

		void UnsubscribeCollectionItemsSourceChanged(ItemsViewAdapter<CarouselView, IItemsViewSource> oldItemViewAdapter)
		{
			if (oldItemViewAdapter != null && oldItemViewAdapter.ItemsSource is ObservableItemsSource oldObservableItemsSource)
				oldObservableItemsSource.CollectionItemsSourceChanged -= CollectionItemsSourceChanged;
		}

		void CollectionItemsSourceChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (!(ItemsViewAdapter.ItemsSource is IItemsViewSource observableItemsSource))
				return;

			var carouselPosition = Carousel.Position;
			var currentItemPosition = observableItemsSource.GetPosition(Carousel.CurrentItem);
			var count = observableItemsSource.Count;

			bool removingCurrentElement = currentItemPosition == -1;
			bool removingLastElement = e.OldStartingIndex == count;
			bool removingFirstElement = e.OldStartingIndex == 0;
			bool removingAnyPrevious =
				e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove
				&& e.OldStartingIndex < carouselPosition;

			_noNeedForScroll = true;
			_gotoPosition = -1;

			if (removingCurrentElement)
			{
				if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
				{
					carouselPosition = 0;
				}

				if (removingFirstElement)
					carouselPosition = 0;
				else if (removingLastElement)
					carouselPosition = Carousel.Position - 1;

				if (Carousel.Loop)
				{
					UpdateAdapter();
					ScrollToPosition(carouselPosition);
				}
			}
			//If we are adding a new item make sure to maintain the CurrentItemPosition
			else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
				&& currentItemPosition != -1)
			{
				carouselPosition = currentItemPosition;
			}

			// Queue the rest up for later after the Adapter has finished processing item change notifications

			if (removingAnyPrevious)
			{
				return;
			}

			Device.BeginInvokeOnMainThread(() =>
			{

				SetCurrentItem(carouselPosition);
				UpdatePosition(carouselPosition);

				//If we are adding or removing the last item we need to update
				//the inset that we give to items so they are centered
				if (e.NewStartingIndex == count - 1 || removingLastElement)
				{
					UpdateItemDecoration();
				}

				UpdateVisualStates();
			});
		}

		void UpdateItemDecoration()
		{
			if (_itemDecoration != null)
				RemoveItemDecoration(_itemDecoration);
			_itemDecoration = CreateSpacingDecoration(ItemsLayout);
			AddItemDecoration(_itemDecoration);
		}

		void UpdateInitialPosition()
		{
			int itemCount = 0;
			int position;

			if (Carousel.CurrentItem != null)
			{
				var carouselEnumerator = ItemsView.ItemsSource.GetEnumerator();
				var items = new List<object>();

				while (carouselEnumerator.MoveNext())
				{
					items.Add(carouselEnumerator.Current);
					itemCount++;
				}

				position = items.IndexOf(Carousel.CurrentItem);
				Carousel.Position = position;
			}
			else
				position = Carousel.Position;

			_oldPosition = position;

			SetCurrentItem(_oldPosition);

			var index = Carousel.Loop ? LoopedPosition(itemCount) + _oldPosition : _oldPosition;
			ScrollHelper.JumpScrollToPosition(index, Microsoft.Maui.Controls.ScrollToPosition.Center);
			_gotoPosition = -1;
		}

		int LoopedPosition(int itemCount)
		{
			if (itemCount == 0)
			{
				return 0;
			}

			var loopScale = CarouselViewLoopManager.LoopScale / 2;
			return loopScale - (loopScale % itemCount);
		}

		void UpdatePositionFromVisibilityChanges()
		{
			if (_isVisible != ItemsView.IsVisible)
				UpdateInitialPosition();

			_isVisible = ItemsView.IsVisible;
		}

		void UpdateVisualStates()
		{
			if (!(GetLayoutManager() is LinearLayoutManager layoutManager))
				return;

			var first = layoutManager.FindFirstVisibleItemPosition();
			var last = layoutManager.FindLastVisibleItemPosition();


			if (first == -1)
				return;

			var newViews = new List<View>();
			var carouselPosition = this.CalculateCenterItemIndex(first, layoutManager, false);
			var previousPosition = carouselPosition - 1;
			var nextPosition = carouselPosition + 1;

			for (int i = first; i <= last; i++)
			{
				var cell = layoutManager.FindViewByPosition(i);
				if (!((cell as ItemContentView)?.View is View itemView))
					return;

				if (i == carouselPosition)
				{
					VisualStateManager.GoToState(itemView, CarouselView.CurrentItemVisualState);
				}
				else if (i == previousPosition)
				{
					VisualStateManager.GoToState(itemView, CarouselView.PreviousItemVisualState);
				}
				else if (i == nextPosition)
				{
					VisualStateManager.GoToState(itemView, CarouselView.NextItemVisualState);
				}
				else
				{
					VisualStateManager.GoToState(itemView, CarouselView.DefaultItemVisualState);
				}

				newViews.Add(itemView);

				if (!Carousel.VisibleViews.Contains(itemView))
				{
					Carousel.VisibleViews.Add(itemView);
				}
			}

			foreach (var itemView in _oldViews)
			{
				if (!newViews.Contains(itemView))
				{
					VisualStateManager.GoToState(itemView, CarouselView.DefaultItemVisualState);
					if (Carousel.VisibleViews.Contains(itemView))
					{
						Carousel.VisibleViews.Remove(itemView);
					}
				}
			}

			_oldViews = newViews;
		}

		void CarouselViewScrolled(object sender, ItemsViewScrolledEventArgs e)
		{
			if (!_initialized || !_isVisible)
				return;

			_noNeedForScroll = false;
			var index = e.CenterItemIndex;
			if (Carousel?.Loop == true)
			{
				index = GetCarouselViewCurrentIndex(index);
			}

			if (index == -1)
				return;

			UpdatePosition(index);
			UpdateVisualStates();
		}

		int GetCarouselViewCurrentIndex(int index)
		{
			var centeredView = this.GetCenteredView();

			if (centeredView is ItemContentView templatedCell)
			{
				var bContext = (templatedCell?.View as VisualElement)?.BindingContext;
				index = ItemsViewAdapter.GetPositionForItem(bContext);
			}
			else
			{
				return -1;
			}

			return index;
		}

		void UpdatePosition(int position)
		{
			var carouselPosition = Carousel.Position;

			// We arrived center
			if (position == _gotoPosition)
				_gotoPosition = -1;

			if (_gotoPosition == -1 && carouselPosition != position)
				Carousel.SetValueFromRenderer(CarouselView.PositionProperty, position);
		}

		void SetCurrentItem(int carouselPosition)
		{
			if (ItemsViewAdapter?.ItemsSource?.Count == 0)
				return;

			var item = ItemsViewAdapter.ItemsSource.GetItem(carouselPosition);
			Carousel.SetValueFromRenderer(CarouselView.CurrentItemProperty, item);
		}

		void IMauiCarouselRecyclerView.UpdateFromCurrentItem()
		{
			var currentItemPosition = ItemsViewAdapter.ItemsSource.GetPosition(Carousel.CurrentItem);
			var carouselPosition = Carousel.Position;

			if (_gotoPosition == -1 && currentItemPosition != carouselPosition)
			{
				_gotoPosition = currentItemPosition;
				ItemsView.ScrollTo(currentItemPosition, position: Microsoft.Maui.Controls.ScrollToPosition.Center, animate: Carousel.AnimateCurrentItemChanges);
			}
		}

		void IMauiCarouselRecyclerView.UpdateFromPosition()
		{
			if (!_initialized)
			{
				_carouselViewLoopManager.AddPendingScrollTo(new ScrollToRequestEventArgs(Carousel.Position, -1, Microsoft.Maui.Controls.ScrollToPosition.Center, false));
			}

			var itemCount = ItemsViewAdapter?.ItemsSource.Count ?? 0;
			var carouselPosition = Carousel.Position;

			if (itemCount == 0)
			{
				//we are trying to set a position but our Collection doesn't have items still
				_oldPosition = carouselPosition;
				return;
			}


			if (carouselPosition >= itemCount || carouselPosition < 0)
				throw new IndexOutOfRangeException($"Can't set CarouselView to position {carouselPosition}. ItemsSource has {itemCount} items.");

			if (carouselPosition == _gotoPosition)
				_gotoPosition = -1;

			if (_noNeedForScroll)
			{
				_noNeedForScroll = false;
				return;
			}

			var centerPosition = GetCarouselViewCurrentIndex(carouselPosition);
			if (_gotoPosition == -1 && !Carousel.IsDragging && !Carousel.IsScrolling && centerPosition != carouselPosition)
			{
				_gotoPosition = carouselPosition;

				ItemsView.ScrollTo(carouselPosition, position: Microsoft.Maui.Controls.ScrollToPosition.Center, animate: Carousel.AnimatePositionChanges);
			}
			SetCurrentItem(carouselPosition);
		}

		void AddLayoutListener()
		{
			if (_carouselViewLayoutListener != null)
				return;

			_carouselViewLayoutListener = new CarouselViewwOnGlobalLayoutListener();
			_carouselViewLayoutListener.LayoutReady += LayoutReady;

			ViewTreeObserver.AddOnGlobalLayoutListener(_carouselViewLayoutListener);
		}

		void LayoutReady(object sender, EventArgs e)
		{
			if (!_initialized)
			{
				ItemsView.Scrolled += CarouselViewScrolled;
				if (Carousel.Loop)
				{
					_carouselViewLoopManager.CenterIfNeeded(this, IsHorizontal);
					_carouselViewLoopManager.CheckPendingScrollToEvents(this);
				}
				_initialized = true;
				_isVisible = ItemsView.IsVisible;
			}

			UpdatePositionFromVisibilityChanges();
			UpdateVisualStates();
		}

		void ClearLayoutListener()
		{
			if (_carouselViewLayoutListener == null)
				return;

			ViewTreeObserver?.RemoveOnGlobalLayoutListener(_carouselViewLayoutListener);
			_carouselViewLayoutListener.LayoutReady -= LayoutReady;
			_carouselViewLayoutListener = null;
		}
	}

	class CarouselViewwOnGlobalLayoutListener : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
	{
		public EventHandler<EventArgs> LayoutReady;
		public void OnGlobalLayout()
		{
			LayoutReady?.Invoke(this, new EventArgs());
		}
	}
}