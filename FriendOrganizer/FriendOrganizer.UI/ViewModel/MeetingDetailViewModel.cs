﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FriendOrganizer.Model;
using FriendOrganizer.UI.Data.Repositories;
using FriendOrganizer.UI.Event;
using FriendOrganizer.UI.View.Services;
using FriendOrganizer.UI.Wrapper;
using Prism.Commands;
using Prism.Events;

namespace FriendOrganizer.UI.ViewModel
{
    public class MeetingDetailViewModel : DetailViewModelBase, IMeetingDetailViewModel
    {
        private readonly IMeetingRepository _meetingRepository;
        private MeetingWrapper _meetingWrapper;
        private Friend _selectedAddedFriend;
        private Friend _selectedAvailableFriend;
        private List<Friend> _allFriends;

        public MeetingDetailViewModel(IEventAggregator eventAggregator,
            IMessageDialogService messageDialogService,
            IMeetingRepository meetingRepository) : base(eventAggregator, messageDialogService) {
            _meetingRepository = meetingRepository;
            eventAggregator.GetEvent<AfterDetailSavedEvent>().Subscribe(AfterDetailSaved);
            eventAggregator.GetEvent<AfterDetailDeletedEvent>().Subscribe(AfterDetailDeleted);

            AddedFriends = new ObservableCollection<Friend>();
            AvailableFriends = new ObservableCollection<Friend>();
            AddFriendCommand = new DelegateCommand(OnAddFriendExecute,OnAddFriendCanExecute);
            RemoveFriendCommand = new DelegateCommand(OnRemoveFriendExecute,OnRemoveFriendCanExecute);
        }

        

        public MeetingWrapper Meeting {
            get => _meetingWrapper;
            set {
                _meetingWrapper = value;
                OnPropertyChanged();
            }
        }
        public Friend SelectedAvailableFriend {
            get => _selectedAvailableFriend;
            set {
                _selectedAvailableFriend = value;
                OnPropertyChanged();
                ((DelegateCommand)AddFriendCommand).RaiseCanExecuteChanged();
            }
        }
        public Friend SelectedAddedFriend {
            get => _selectedAddedFriend;
            set {
                _selectedAddedFriend = value;
                OnPropertyChanged();
                ((DelegateCommand)RemoveFriendCommand).RaiseCanExecuteChanged();
            }
        }
        public ObservableCollection<Friend> AddedFriends { get; }
        public ObservableCollection<Friend> AvailableFriends { get; }

        public ICommand AddFriendCommand { get; }
        public ICommand RemoveFriendCommand { get; }

        public override async Task LoadAsync(int meetingId) {
            var meeting = meetingId > 0
                ? await _meetingRepository.GetByIdAsync(meetingId)
                : CreateNewMeeting();

            Id = meetingId;
            InitializeMeeting(meeting);
            _allFriends = await _meetingRepository.GetAllFriendsAsync();
            SetupPicklist();
        }

        protected override void OnDeleteExecute() {
            var result = MessageDialogService.ShowOkCancelDialog($"Do you really want to delete the meeting {Meeting.Title}?", "Question");
            if (result == MessageDialogResult.Ok) {
                _meetingRepository.Remove(Meeting.Model);
                _meetingRepository.SaveAsync();
                RaiseDetailDeletedEvent(Meeting.Id);
            }
        }
        protected override bool OnSaveCanExecute() {
            return Meeting != null && !Meeting.HasErrors && HasChanges;
        }

        protected override void OnSaveExecute() {
            _meetingRepository.SaveAsync();
            Id = Meeting.Id;
            HasChanges = _meetingRepository.HasChanges();
            RaiseDetailSavedEvent(Meeting.Id,Meeting.Title);
        }

        private Meeting CreateNewMeeting()
        {
            var meeting = new Meeting
            {
                DateFrom = DateTime.Now.Date,
                DateTo = DateTime.Now.Date
            };

            _meetingRepository.Add(meeting);
            return meeting;
        }

        private void InitializeMeeting(Meeting meeting)
        {
            Meeting = new MeetingWrapper(meeting);

            Meeting.PropertyChanged += (s, e) => {
                if (!HasChanges)
                    HasChanges = _meetingRepository.HasChanges();

                if (e.PropertyName == nameof(Meeting.HasErrors))
                    ((DelegateCommand)SaveCommand).RaiseCanExecuteChanged();

                if (e.PropertyName == nameof(Meeting.Title))
                    SetTitle();
            };
            ((DelegateCommand)SaveCommand).RaiseCanExecuteChanged();

            //Trick to validate the validation.
            if (Meeting.Id == 0)
                Meeting.Title = "";

            SetTitle();
        }

        private void SetTitle() {
            Title = $"{Meeting.Title}";
        }

        private bool OnRemoveFriendCanExecute() {
            return SelectedAddedFriend != null;
        }

        private void OnRemoveFriendExecute() {
            var friendToRemove = SelectedAddedFriend;
            Meeting.Model.Friends.Remove(friendToRemove);
            AddedFriends.Remove(friendToRemove);
            AvailableFriends.Add(friendToRemove);
            HasChanges = _meetingRepository.HasChanges();
            ((DelegateCommand)SaveCommand).RaiseCanExecuteChanged();
        }

        private bool OnAddFriendCanExecute() {
            return SelectedAvailableFriend != null;
        }

        private void OnAddFriendExecute() {
            var friendtoAdd = SelectedAvailableFriend;
            Meeting.Model.Friends.Add(friendtoAdd);
            AddedFriends.Add(friendtoAdd);
            AvailableFriends.Remove(friendtoAdd);
            HasChanges = _meetingRepository.HasChanges();
            ((DelegateCommand)SaveCommand).RaiseCanExecuteChanged();
        }

        private void SetupPicklist() {
            var meetingFriendsIds = Meeting.Model.Friends.Select(f => f.Id).ToList();
            var addedFriends = _allFriends.Where(f => meetingFriendsIds.Contains(f.Id)).OrderBy(f => f.FirstName);
            var availableFriends = _allFriends.Except(addedFriends).OrderBy(f => f.FirstName);

            AddedFriends.Clear();
            AvailableFriends.Clear();
            foreach (var addedFriend in addedFriends)
                AddedFriends.Add(addedFriend);

            foreach (var availableFriend in availableFriends)
                AvailableFriends.Add(availableFriend);
        }

        private async void  AfterDetailSaved(AfterDetailSavedEventArgs args) {
            if (args.ViewModelName == nameof(FriendDetailViewModel)) {
                 await _meetingRepository.ReloadFriendAsync(args.Id);
                _allFriends = await _meetingRepository.GetAllFriendsAsync();
                SetupPicklist();
            }
        }

        private async void AfterDetailDeleted(AfterDetailDeletedEventArgs obj) {
            if (obj.ViewModelName == nameof(FriendDetailViewModel)) {
                _allFriends = await _meetingRepository.GetAllFriendsAsync();
                SetupPicklist();
            }
        }
    }
}
