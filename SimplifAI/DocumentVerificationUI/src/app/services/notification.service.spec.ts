import { TestBed } from '@angular/core/testing';
import { NotificationService, NotificationMessage } from './notification.service';

describe('NotificationService', () => {
  let service: NotificationService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(NotificationService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should show success notification', () => {
    let receivedNotification: NotificationMessage | null = null;
    
    service.notifications$.subscribe(notification => {
      receivedNotification = notification;
    });

    service.showSuccess('Success Title', 'Success message');

    expect(receivedNotification).toBeTruthy();
    expect(receivedNotification?.type).toBe('success');
    expect(receivedNotification?.title).toBe('Success Title');
    expect(receivedNotification?.message).toBe('Success message');
    expect(receivedNotification?.autoClose).toBe(true);
  });

  it('should show error notification', () => {
    let receivedNotification: NotificationMessage | null = null;
    
    service.notifications$.subscribe(notification => {
      receivedNotification = notification;
    });

    service.showError('Error Title', 'Error message');

    expect(receivedNotification).toBeTruthy();
    expect(receivedNotification?.type).toBe('error');
    expect(receivedNotification?.title).toBe('Error Title');
    expect(receivedNotification?.message).toBe('Error message');
    expect(receivedNotification?.autoClose).toBe(false);
  });

  it('should show warning notification', () => {
    let receivedNotification: NotificationMessage | null = null;
    
    service.notifications$.subscribe(notification => {
      receivedNotification = notification;
    });

    service.showWarning('Warning Title', 'Warning message');

    expect(receivedNotification).toBeTruthy();
    expect(receivedNotification?.type).toBe('warning');
    expect(receivedNotification?.title).toBe('Warning Title');
    expect(receivedNotification?.message).toBe('Warning message');
    expect(receivedNotification?.autoClose).toBe(true);
  });

  it('should show info notification', () => {
    let receivedNotification: NotificationMessage | null = null;
    
    service.notifications$.subscribe(notification => {
      receivedNotification = notification;
    });

    service.showInfo('Info Title', 'Info message');

    expect(receivedNotification).toBeTruthy();
    expect(receivedNotification?.type).toBe('info');
    expect(receivedNotification?.title).toBe('Info Title');
    expect(receivedNotification?.message).toBe('Info message');
    expect(receivedNotification?.autoClose).toBe(true);
  });

  it('should clear notification', () => {
    let notifications: NotificationMessage[] = [];
    
    service.notifications$.subscribe(notification => {
      if (notification) {
        notifications.push(notification);
      } else {
        notifications = [];
      }
    });

    service.showSuccess('Test', 'Test message');
    expect(notifications.length).toBe(1);

    service.clear();
    expect(notifications.length).toBe(0);
  });

  it('should generate unique IDs for notifications', () => {
    const notifications: NotificationMessage[] = [];
    
    service.notifications$.subscribe(notification => {
      if (notification) {
        notifications.push(notification);
      }
    });

    service.showSuccess('Test 1', 'Message 1');
    service.showError('Test 2', 'Message 2');

    expect(notifications.length).toBe(2);
    expect(notifications[0].id).not.toBe(notifications[1].id);
  });

  it('should handle notifications with custom duration', () => {
    let receivedNotification: NotificationMessage | null = null;
    
    service.notifications$.subscribe(notification => {
      receivedNotification = notification;
    });

    service.showSuccess('Test', 'Test message', 10000);

    expect(receivedNotification?.duration).toBe(10000);
  });

  it('should handle notifications without message', () => {
    let receivedNotification: NotificationMessage | null = null;
    
    service.notifications$.subscribe(notification => {
      receivedNotification = notification;
    });

    service.showSuccess('Test Title');

    expect(receivedNotification?.title).toBe('Test Title');
    expect(receivedNotification?.message).toBe('');
  });
});