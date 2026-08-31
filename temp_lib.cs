                IsScanRunning = false;
            }
        }

        public async Task ValidateTopLibraryFolders(CancellationToken cancellationToken, bool removeRoot = false)
        {
            ClearIgnoreRuleCache();
            RootFolder.Children = null;
            await RootFolder.RefreshMetadata(cancellationToken).ConfigureAwait(false);

            // Start by just validating the children of the root, but go no further
            await RootFolder.ValidateChildren(
                new Progress<double>(),
                new MetadataRefreshOptions(new DirectoryService(_fileSystem)),
                recursive: false,
                allowRemoveRoot: removeRoot,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var rootFolder = GetUserRootFolder();
            rootFolder.Children = null;

            await rootFolder.RefreshMetadata(cancellationToken).ConfigureAwait(false);

            await rootFolder.ValidateChildren(
                new Progress<double>(),
                new MetadataRefreshOptions(new DirectoryService(_fileSystem)),
                recursive: false,
                allowRemoveRoot: removeRoot,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Quickly scan CollectionFolders for changes
            var toDelete = new List<Guid>();
            foreach (var child in rootFolder.Children!.OfType<Folder>())
            {
                // If the user has somehow deleted the collection directory, remove the metadata from the database.
                if (child is CollectionFolder collectionFolder && !Directory.Exists(collectionFolder.Path))
                {
                    toDelete.Add(collectionFolder.Id);
                }
                else
                {
                    await child.RefreshMetadata(cancellationToken).ConfigureAwait(false);
                }
            }

            if (toDelete.Count > 0)
            {
                _persistenceService.DeleteItem(toDelete.ToArray());
            }

            ClearIgnoreRuleCache();
