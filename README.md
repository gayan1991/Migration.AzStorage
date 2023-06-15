# Migration for Azure Storage Accounts
Use this solution to migrate Azure Storage Account between multiple regions

At the moment it is built for blob storage only.

In migration config file, you will see a json as follows.

```
{
  "storageAccounts": [
    {
      "sourceResourceGroup": "ResourceGroup",
      "sourceUrl": "https://store1.blob.core.windows.net/",
      "targetResourceGroup": "ResourceGroup2",
      "targetUrl": "https://store2.blob.core.windows.net/",
      "filter": {
        "type": 1, // Last modified
        "value": "2022-01-01"
      }
    },{
      "sourceResourceGroup": "ResourceGroup",
      "sourceUrl": "https://store1.blob.core.windows.net/",
      "targetResourceGroup": "ResourceGroup2",
      "targetUrl": "https://store2.blob.core.windows.net/",
      "filter": {
        "type": 0, // Name
        "value": "storageAccoutnt,storageAccount2"
      }
    }
  ],
  "sourceSubscription": "subscriptionName1",
  "targetSubscription": "subscriptionName2"
}
```

You can fill number of storage accounts as you want, but it is built to migrate from one subscription to another. If you have to use AzCopy and need to migrate multiple accoutns, this would be ideal for you. Moreover, there is container filteration where you can filter by name or last mofified date.

Enjoy
