using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class DayCycleManager : MonoBehaviour
    {
        private readonly struct CustomerDefinition
        {
            public CustomerDefinition(string id, string displayName, LetterStamp requiredStamps)
            {
                Id = id;
                DisplayName = displayName;
                RequiredStamps = requiredStamps;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public LetterStamp RequiredStamps { get; }
        }

        [SerializeField] private LetterTaskState taskState;
        [SerializeField] private TestCustomerNPC customer;
        [SerializeField, Min(0f)] private float nextCustomerDelay = 2f;

        private readonly List<CustomerDefinition> dayOrder = new();
        private int nextCustomerIndex;
        private bool transitionInProgress;
        private bool customerActive;

        private void Start()
        {
            PrepareDay();
        }

        public void NotifyCustomerDeparted()
        {
            if (!customerActive || taskState == null || !taskState.LetterMailed)
                return;

            customerActive = false;
            taskState.CompleteCurrentRequest();
            if (!taskState.RequiredDayComplete)
                StartCoroutine(BeginNextCustomerAfterDelay());
        }

        public bool TryAdvanceDay()
        {
            if (transitionInProgress || taskState == null || !taskState.RequiredDayComplete)
                return false;

            transitionInProgress = true;
            StopAllCoroutines();
            if (customer != null)
                customer.HideUntilNextCustomer();
            taskState.BeginNextDay();
            PrepareDay();
            transitionInProgress = false;
            return true;
        }

        private void PrepareDay()
        {
            dayOrder.Clear();
            dayOrder.Add(new CustomerDefinition("mascot", "인형탈", LetterStamp.Fire | LetterStamp.Shock));
            dayOrder.Add(new CustomerDefinition("drunk_driver", "음주운전", LetterStamp.Shock | LetterStamp.Alcohol));
            dayOrder.Add(new CustomerDefinition("model_student", "모범생", LetterStamp.Medicine));
            dayOrder.Add(new CustomerDefinition("bad_construction", "부실공사", LetterStamp.Electricity | LetterStamp.Shock));
            dayOrder.Add(new CustomerDefinition("troublemaker", "불량이", LetterStamp.Water));

            for (int index = dayOrder.Count - 1; index > 0; index--)
            {
                int swapIndex = Random.Range(0, index + 1);
                (dayOrder[index], dayOrder[swapIndex]) = (dayOrder[swapIndex], dayOrder[index]);
            }

            nextCustomerIndex = 0;
            customerActive = false;
            BeginNextCustomer();
        }

        private IEnumerator BeginNextCustomerAfterDelay()
        {
            yield return new WaitForSeconds(nextCustomerDelay);
            BeginNextCustomer();
        }

        private void BeginNextCustomer()
        {
            if (customer == null || taskState == null || taskState.RequiredDayComplete || nextCustomerIndex >= dayOrder.Count)
                return;

            CustomerDefinition definition = dayOrder[nextCustomerIndex++];
            string letterId = $"day_{taskState.CurrentDay:00}_{definition.Id}";
            customerActive = true;
            customer.BeginCustomer(definition.Id, definition.DisplayName, letterId, definition.RequiredStamps, this);
        }
    }
}
